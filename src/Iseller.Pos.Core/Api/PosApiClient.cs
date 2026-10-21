using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Iseller.Pos.Core.Modelos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Iseller.Pos.Core.Api
{
    public sealed class SucursalOpcion
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
    }

    public sealed class ResultadoLogin
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public bool RequiereSucursal { get; set; }
        public List<SucursalOpcion> Sucursales { get; set; }
        public SesionLocal Sesion { get; set; }
        public bool MembresiaVencida { get; set; }
    }

    public sealed class ResultadoCatalogo
    {
        public bool Ok { get; set; }
        public bool SinCambios { get; set; }
        public string Error { get; set; }
        public string Version { get; set; }
        public DateTimeOffset ServidorEn { get; set; }
        public List<Producto> Productos { get; set; }
        public List<Cliente> Clientes { get; set; }
        public Tasas Tasas { get; set; }
        public ConfiguracionNegocio Configuracion { get; set; }
    }

    public sealed class ResultadoVentaEnvio
    {
        public string IdPedido { get; set; }
        public bool Ok { get; set; }
        public int OrdenId { get; set; }
        public bool Duplicada { get; set; }
        public string Error { get; set; }
    }

    public sealed class ResultadoEnvio
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public DateTimeOffset ServidorEn { get; set; }
        public List<ResultadoVentaEnvio> Resultados { get; set; }

        public ResultadoEnvio()
        {
            Resultados = new List<ResultadoVentaEnvio>();
        }
    }

    public sealed class ResultadoCaja
    {
        public bool Ok { get; set; }
        public bool Duplicada { get; set; }
        public int CorteId { get; set; }
        public string Error { get; set; }
        public DateTimeOffset ServidorEn { get; set; }
    }

    public sealed class ResultadoReapertura
    {
        public bool Ok { get; set; }
        public bool YaEstaba { get; set; }
        public string Error { get; set; }
    }

    public sealed class ResultadoAnulacion
    {
        public bool Ok { get; set; }
        public bool YaAnulada { get; set; }
        public string Error { get; set; }
    }

    /// <summary>Cliente de los endpoints api/pos_*.php (X-Api-Key + Bearer).</summary>
    public sealed class PosApiClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public PosApiClient(string baseUrl, string apiKey, HttpMessageHandler handler = null)
        {
            // Windows 7 no activa TLS 1.2 por defecto.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _apiKey = apiKey ?? "";
            _http = handler == null ? new HttpClient() : new HttpClient(handler);
            _http.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<ResultadoLogin> LoginAsync(string usuario, string password, int? sucursalId)
        {
            var cuerpo = new JObject
            {
                ["usuario"] = usuario,
                ["password"] = password,
            };
            if (sucursalId.HasValue)
            {
                cuerpo["sucursal_id"] = sucursalId.Value;
            }

            var resultado = new ResultadoLogin { Sucursales = new List<SucursalOpcion>() };

            using (var respuesta = await EnviarAsync("pos_login.php", HttpMethod.Post, cuerpo, null))
            {
                string texto = await respuesta.Content.ReadAsStringAsync();
                var dto = Deserializar<LoginDto>(texto);
                if (dto == null)
                {
                    resultado.Error = "Respuesta inválida del servidor";
                    return resultado;
                }

                resultado.Error = dto.Error;
                resultado.RequiereSucursal = respuesta.StatusCode == HttpStatusCode.Conflict;
                resultado.MembresiaVencida = dto.Renovacion;

                if (dto.Sucursales != null)
                {
                    foreach (var s in dto.Sucursales)
                    {
                        resultado.Sucursales.Add(new SucursalOpcion { Id = s.Id, Nombre = s.Nombre });
                    }
                }

                if (respuesta.IsSuccessStatusCode && dto.Ok)
                {
                    resultado.Ok = true;
                    resultado.Sesion = new SesionLocal
                    {
                        Token = dto.Token,
                        UsuarioId = dto.Usuario != null ? dto.Usuario.Id : 0,
                        Nombre = dto.Usuario != null ? dto.Usuario.Nombre : "",
                        Usuario = dto.Usuario != null ? dto.Usuario.Usuario : usuario,
                        Nivel = dto.Usuario != null ? dto.Usuario.Nivel : 2,
                        BssId = dto.BssId,
                        SucursalId = dto.Sucursal != null ? dto.Sucursal.Id : 0,
                        SucursalNombre = dto.Sucursal != null ? dto.Sucursal.Nombre : "",
                        Permisos = dto.Permisos ?? new List<int>(),
                        PuedeVenderAlMayor = dto.PuedeMayor,
                        ExpiraEnSegundos = dto.ExpiraEn,
                        UltimoLoginValidoEn = dto.ServidorEn,
                        Configuracion = new ConfiguracionNegocio
                        {
                            Tickets = dto.Configuracion != null ? dto.Configuracion.Tickets : 0,
                            BsTicket = dto.Configuracion != null ? dto.Configuracion.BsTicket : 0,
                            CortesCaja = dto.Configuracion != null ? dto.Configuracion.CortesCaja : 0,
                            RegistroClientes = dto.Configuracion != null ? dto.Configuracion.RegistroClientes : 0,
                        },
                    };
                }
            }

            return resultado;
        }

        public async Task<ResultadoCatalogo> DescargarCatalogoAsync(string token, string versionActual)
        {
            var resultado = new ResultadoCatalogo
            {
                Productos = new List<Producto>(),
                Clientes = new List<Cliente>(),
            };

            string url = "pos_catalogo.php";
            if (!string.IsNullOrEmpty(versionActual))
            {
                url += "?v=" + Uri.EscapeDataString(versionActual);
            }

            using (var respuesta = await EnviarAsync(url, HttpMethod.Get, null, token))
            {
                if (respuesta.StatusCode == HttpStatusCode.NotModified)
                {
                    resultado.Ok = true;
                    resultado.SinCambios = true;
                    return resultado;
                }

                string texto = await respuesta.Content.ReadAsStringAsync();
                var dto = Deserializar<CatalogoDto>(texto);
                if (dto == null || !dto.Ok)
                {
                    resultado.Error = dto != null && !string.IsNullOrEmpty(dto.Error)
                        ? dto.Error
                        : "No se pudo descargar el catálogo";
                    return resultado;
                }

                resultado.Ok = true;
                resultado.Version = dto.Version;
                resultado.ServidorEn = dto.ServidorEn;
                resultado.Tasas = ADtoTasas(dto.Tasas, dto.ServidorEn);
                resultado.Configuracion = new ConfiguracionNegocio
                {
                    Tickets = dto.Configuracion != null ? dto.Configuracion.Tickets : 0,
                    BsTicket = dto.Configuracion != null ? dto.Configuracion.BsTicket : 0,
                    CortesCaja = dto.Configuracion != null ? dto.Configuracion.CortesCaja : 0,
                    RegistroClientes = dto.Configuracion != null ? dto.Configuracion.RegistroClientes : 0,
                };

                foreach (var p in dto.Productos ?? new List<ProductoDto>())
                {
                    var producto = new Producto
                    {
                        Id = p.Id,
                        Nombre = p.Nombre ?? "",
                        CodigoBarras = p.CodigoBarras ?? "",
                        Origen = string.IsNullOrEmpty(p.Origen) ? "v" : p.Origen,
                        PrecioCompra = p.PrecioCompra,
                        CantidadUnidades = p.CantidadUnidades,
                        Porcentaje = p.Porcentaje,
                        Mayor = p.Mayor ?? "0",
                        EsCombo = p.EsCombo,
                        Foto = p.Foto ?? "",
                        Stock = p.Stock,
                    };
                    foreach (var item in p.ComboItems ?? new List<ItemComboDto>())
                    {
                        producto.ComboItems.Add(new ItemCombo
                        {
                            Nombre = item.Nombre ?? "",
                            Cantidad = item.Cantidad,
                        });
                    }
                    resultado.Productos.Add(producto);
                }

                foreach (var c in dto.Clientes ?? new List<ClienteDto>())
                {
                    resultado.Clientes.Add(new Cliente
                    {
                        Cedula = c.Cedula ?? "",
                        Nombre = c.Nombre ?? "",
                        Telefono = c.Telefono ?? "",
                    });
                }
            }

            return resultado;
        }

        public async Task<Tasas> DescargarTasasAsync(string token)
        {
            using (var respuesta = await EnviarAsync("pos_tasas.php", HttpMethod.Get, null, token))
            {
                string texto = await respuesta.Content.ReadAsStringAsync();
                var dto = Deserializar<TasasRespuestaDto>(texto);
                if (dto == null || !dto.Ok)
                {
                    return null;
                }
                return ADtoTasas(dto.Tasas, dto.ServidorEn);
            }
        }

        /// <summary>Envía un lote de ventas pendientes. El servidor es idempotente por id_pedido.</summary>
        public async Task<ResultadoEnvio> EnviarVentasAsync(string token, IList<Venta> ventas)
        {
            var resultado = new ResultadoEnvio();
            if (ventas == null || ventas.Count == 0)
            {
                resultado.Ok = true;
                return resultado;
            }

            var lote = new JArray();
            foreach (var venta in ventas)
            {
                var item = new JObject
                {
                    ["id_pedido"] = venta.IdPedido,
                    ["fecha_local"] = venta.FechaLocal.ToString("o"),
                    ["metodo_pago"] = venta.MetodoPago,
                    ["despacho"] = venta.Despacho,
                    ["total_usd"] = venta.TotalUsd,
                    ["total_bs"] = venta.TotalBs,
                    ["total_cop"] = venta.TotalCop,
                    ["cliente"] = new JObject
                    {
                        ["cedula"] = venta.ClienteCedula ?? "",
                        ["nombre"] = venta.ClienteNombre ?? "",
                        ["telefono"] = venta.ClienteTelefono ?? "",
                    },
                    ["usuario_id"] = venta.UsuarioId,
                    ["tasas"] = string.IsNullOrWhiteSpace(venta.TasasJson) ? new JObject() : JObject.Parse(venta.TasasJson),
                    ["productos"] = string.IsNullOrWhiteSpace(venta.ProductosJson) ? new JArray() : JArray.Parse(venta.ProductosJson),
                };

                // Pago fraccionado (option8): el desglose por medio de pago.
                if (!string.IsNullOrWhiteSpace(venta.PagosJson))
                {
                    item["pagos_fraccionados"] = JObject.Parse(venta.PagosJson);
                }

                lote.Add(item);
            }

            var cuerpo = new JObject
            {
                ["lote_id"] = Guid.NewGuid().ToString(),
                ["ventas"] = lote,
            };

            using (var respuesta = await EnviarAsync("pos_ventas.php", HttpMethod.Post, cuerpo, token))
            {
                string texto = await respuesta.Content.ReadAsStringAsync();
                var dto = Deserializar<EnvioDto>(texto);
                if (dto == null)
                {
                    resultado.Error = "Respuesta inválida del servidor";
                    return resultado;
                }

                resultado.Ok = dto.Ok;
                resultado.Error = dto.Error;
                resultado.ServidorEn = dto.ServidorEn;

                foreach (var r in dto.Resultados ?? new List<ResultadoDto>())
                {
                    resultado.Resultados.Add(new ResultadoVentaEnvio
                    {
                        IdPedido = r.IdPedido,
                        Ok = r.Ok,
                        OrdenId = r.OrdenId,
                        Duplicada = r.Duplicada,
                        Error = r.Error,
                    });
                }
            }

            return resultado;
        }

        /// <summary>Guarda una apertura o cierre (idempotente por UUID).</summary>
        public async Task<ResultadoCaja> GuardarCajaAsync(string token, RegistroCaja registro)
        {
            var cuerpo = new JObject
            {
                ["accion"] = "guardar",
                ["id"] = registro.Id,
                ["tipo"] = registro.Tipo,
                ["fecha"] = registro.FechaLocal,
                ["montos"] = JObject.FromObject(registro.Montos ?? new Dictionary<string, double>()),
                ["observaciones"] = registro.Observaciones ?? "",
            };

            using (var respuesta = await EnviarAsync("pos_caja.php", HttpMethod.Post, cuerpo, token))
            {
                string texto = await respuesta.Content.ReadAsStringAsync();
                var dto = Deserializar<CajaRespuestaDto>(texto);
                if (dto == null)
                {
                    return new ResultadoCaja { Error = "Respuesta inválida del servidor" };
                }
                return new ResultadoCaja
                {
                    Ok = dto.Ok,
                    Duplicada = dto.Duplicada,
                    CorteId = dto.CorteId,
                    Error = dto.Error,
                    ServidorEn = dto.ServidorEn,
                };
            }
        }

        /// <summary>Reabre una caja borrando su cierre en el servidor (requiere red).</summary>
        public async Task<ResultadoReapertura> ReabrirCajaAsync(string token, string corteId)
        {
            var cuerpo = new JObject
            {
                ["accion"] = "reabrir",
                ["corte_id"] = corteId,
            };

            using (var respuesta = await EnviarAsync("pos_caja.php", HttpMethod.Post, cuerpo, token))
            {
                string texto = await respuesta.Content.ReadAsStringAsync();
                var dto = Deserializar<ReaperturaDto>(texto);
                if (dto == null)
                {
                    return new ResultadoReapertura { Error = "Respuesta inválida del servidor" };
                }
                return new ResultadoReapertura
                {
                    Ok = dto.Ok,
                    YaEstaba = dto.YaEstaba,
                    Error = dto.Error,
                };
            }
        }

        /// <summary>Anula en el servidor una venta ya sincronizada (devuelve el stock).</summary>
        public async Task<ResultadoAnulacion> AnularVentaAsync(string token, string idPedido)
        {
            var cuerpo = new JObject
            {
                ["id_pedido"] = idPedido,
            };

            using (var respuesta = await EnviarAsync("pos_venta_anular.php", HttpMethod.Post, cuerpo, token))
            {
                string texto = await respuesta.Content.ReadAsStringAsync();
                var dto = Deserializar<AnulacionDto>(texto);
                if (dto == null)
                {
                    return new ResultadoAnulacion { Error = "Respuesta inválida del servidor" };
                }
                return new ResultadoAnulacion
                {
                    Ok = dto.Ok,
                    YaAnulada = dto.YaAnulada,
                    Error = dto.Error,
                };
            }
        }

        /// <summary>Comprueba internet y que el token siga vivo. Null si falló.</summary>
        public async Task<DateTimeOffset?> HeartbeatAsync(string token)
        {
            try
            {
                using (var respuesta = await EnviarAsync("pos_heartbeat.php", HttpMethod.Get, null, token))
                {
                    if (!respuesta.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    string texto = await respuesta.Content.ReadAsStringAsync();
                    var dto = Deserializar<HeartbeatDto>(texto);
                    return dto != null && dto.Ok ? dto.ServidorEn : (DateTimeOffset?)null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Tasas ADtoTasas(TasasDto dto, DateTimeOffset servidorEn)
        {
            if (dto == null)
            {
                return null;
            }

            var monedas = dto.DataMonedas ?? new JObject();
            var claves = new HashSet<string>(monedas.Properties().Select(p => p.Name));

            return new Tasas
            {
                PesoDolar = dto.PesoDolar,
                PesoBolivar = dto.PesoBolivar,
                DolarBolivar = dto.DolarBolivar,
                BolivarPeso = dto.BolivarPeso,
                Bcv = dto.Bcv,
                DataMonedasJson = monedas.ToString(Formatting.None),
                PrecioDolarVisible = claves.Contains("precio_dolar_visible"),
                PrecioPesoVisible = claves.Contains("precio_peso_visible"),
                ActualizadoEn = servidorEn,
            };
        }

        private async Task<HttpResponseMessage> EnviarAsync(string ruta, HttpMethod metodo, JObject cuerpo, string token)
        {
            var peticion = new HttpRequestMessage(metodo, _baseUrl + "/" + ruta);
            if (!string.IsNullOrEmpty(_apiKey))
            {
                peticion.Headers.Add("X-Api-Key", _apiKey);
            }
            if (!string.IsNullOrEmpty(token))
            {
                peticion.Headers.Add("Authorization", "Bearer " + token);
            }
            if (cuerpo != null)
            {
                peticion.Content = new StringContent(cuerpo.ToString(Formatting.None), Encoding.UTF8, "application/json");
            }
            return await _http.SendAsync(peticion);
        }

        private static T Deserializar<T>(string texto) where T : class
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(texto);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        // ── DTOs ───────────────────────────────────────────────────────────────────

        private sealed class LoginDto
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("token")] public string Token { get; set; }
            [JsonProperty("expira_en")] public int ExpiraEn { get; set; }
            [JsonProperty("usuario")] public UsuarioDto Usuario { get; set; }
            [JsonProperty("bss_id")] public int BssId { get; set; }
            [JsonProperty("sucursal")] public SucursalDto Sucursal { get; set; }
            [JsonProperty("permisos")] public List<int> Permisos { get; set; }
            [JsonProperty("puede_vender_al_mayor")] public bool PuedeMayor { get; set; }
            [JsonProperty("configuracion")] public ConfiguracionDto Configuracion { get; set; }
            [JsonProperty("servidor_en")] public DateTimeOffset ServidorEn { get; set; }
            [JsonProperty("sucursales")] public List<SucursalDto> Sucursales { get; set; }
            [JsonProperty("renovacion")] public bool Renovacion { get; set; }
        }

        private sealed class UsuarioDto
        {
            [JsonProperty("id")] public int Id { get; set; }
            [JsonProperty("nombre")] public string Nombre { get; set; }
            [JsonProperty("usuario")] public string Usuario { get; set; }
            [JsonProperty("nivel")] public int Nivel { get; set; }
        }

        private sealed class SucursalDto
        {
            [JsonProperty("id")] public int Id { get; set; }
            [JsonProperty("nombre")] public string Nombre { get; set; }
        }

        private sealed class ConfiguracionDto
        {
            [JsonProperty("tickets")] public int Tickets { get; set; }
            [JsonProperty("bs_ticket")] public int BsTicket { get; set; }
            [JsonProperty("cortes_caja")] public int CortesCaja { get; set; }
            [JsonProperty("registro_clientes")] public int RegistroClientes { get; set; }
        }

        private sealed class CatalogoDto
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("version")] public string Version { get; set; }
            [JsonProperty("servidor_en")] public DateTimeOffset ServidorEn { get; set; }
            [JsonProperty("tasas")] public TasasDto Tasas { get; set; }
            [JsonProperty("configuracion")] public ConfiguracionDto Configuracion { get; set; }
            [JsonProperty("productos")] public List<ProductoDto> Productos { get; set; }
            [JsonProperty("clientes")] public List<ClienteDto> Clientes { get; set; }
        }

        private sealed class ProductoDto
        {
            [JsonProperty("id")] public int Id { get; set; }
            [JsonProperty("nombre")] public string Nombre { get; set; }
            [JsonProperty("codigo_barras")] public string CodigoBarras { get; set; }
            [JsonProperty("origen")] public string Origen { get; set; }
            [JsonProperty("precio_compra")] public double PrecioCompra { get; set; }
            [JsonProperty("cantidad_unidades")] public double CantidadUnidades { get; set; }
            [JsonProperty("porcentaje")] public double Porcentaje { get; set; }
            [JsonProperty("mayor")] public string Mayor { get; set; }
            [JsonProperty("es_combo")] public int EsCombo { get; set; }
            [JsonProperty("foto")] public string Foto { get; set; }
            [JsonProperty("stock")] public double Stock { get; set; }
            [JsonProperty("combo_items")] public List<ItemComboDto> ComboItems { get; set; }
        }

        private sealed class ItemComboDto
        {
            [JsonProperty("nombre")] public string Nombre { get; set; }
            [JsonProperty("cantidad")] public double Cantidad { get; set; }
        }

        private sealed class ClienteDto
        {
            [JsonProperty("cedula")] public string Cedula { get; set; }
            [JsonProperty("nombre")] public string Nombre { get; set; }
            [JsonProperty("telefono")] public string Telefono { get; set; }
        }

        private sealed class TasasDto
        {
            [JsonProperty("peso_dolar")] public double PesoDolar { get; set; }
            [JsonProperty("peso_bolivar")] public double PesoBolivar { get; set; }
            [JsonProperty("dolar_bolivar")] public double DolarBolivar { get; set; }
            [JsonProperty("bolivar_peso")] public double BolivarPeso { get; set; }
            [JsonProperty("bcv")] public double Bcv { get; set; }
            [JsonProperty("data_monedas")] public JObject DataMonedas { get; set; }
        }

        private sealed class TasasRespuestaDto
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("servidor_en")] public DateTimeOffset ServidorEn { get; set; }
            [JsonProperty("tasas")] public TasasDto Tasas { get; set; }
        }

        private sealed class HeartbeatDto
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("servidor_en")] public DateTimeOffset ServidorEn { get; set; }
        }

        private sealed class EnvioDto
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("servidor_en")] public DateTimeOffset ServidorEn { get; set; }
            [JsonProperty("resultados")] public List<ResultadoDto> Resultados { get; set; }
        }

        private sealed class ResultadoDto
        {
            [JsonProperty("id_pedido")] public string IdPedido { get; set; }
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("orden_id")] public int OrdenId { get; set; }
            [JsonProperty("duplicada")] public bool Duplicada { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
        }

        private sealed class CajaRespuestaDto
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("duplicada")] public bool Duplicada { get; set; }
            [JsonProperty("corte_id")] public int CorteId { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
            [JsonProperty("servidor_en")] public DateTimeOffset ServidorEn { get; set; }
        }

        private sealed class ReaperturaDto
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("ya_estaba")] public bool YaEstaba { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
        }

        private sealed class AnulacionDto
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("ya_anulada")] public bool YaAnulada { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
        }
    }
}
