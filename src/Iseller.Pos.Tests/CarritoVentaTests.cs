using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Iseller.Pos.Core.Carrito;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Impresion;
using Iseller.Pos.Core.Modelos;
using Iseller.Pos.Core.Precios;
using Xunit;

namespace Iseller.Pos.Tests
{
    public class CarritoTests
    {
        private static CalculadoraPrecios Calculadora()
        {
            return new CalculadoraPrecios(new ParametrosPrecios
            {
                PesoDolar = 4000,
                PesoBolivar = 0.0058,
                DolarBolivar = 1000,
                BolivarPeso = 0.0043,
                Bcv = 1000,
                PrecioDolarVisible = false,
                PrecioPesoVisible = false,
            });
        }

        private static Producto Producto(int id, string nombre, double compra, double unidades, double porcentaje, string mayor = "0")
        {
            return new Producto
            {
                Id = id,
                Nombre = nombre,
                Origen = "v",
                PrecioCompra = compra,
                CantidadUnidades = unidades,
                Porcentaje = porcentaje,
                Mayor = mayor,
                Stock = 10,
            };
        }

        [Fact]
        public void Agrega_y_acumula_cantidades()
        {
            var carrito = new Carrito();
            var calc = Calculadora();
            var producto = Producto(1, "Arroz", 10, 1, 30); // $13 / 13.000 Bs / 55.900 Cop

            carrito.Agregar(producto, calc, 2);
            carrito.Agregar(producto, calc, 3);

            Assert.Equal(1, carrito.CantidadItems);
            Assert.Equal(5, carrito.Obtener(1).Qty);
            Assert.Equal(65, carrito.TotalUsd, 6);
            Assert.Equal(65000, carrito.TotalBs, 6);
            Assert.Equal(279500, carrito.TotalPeso, 6);
        }

        [Fact]
        public void Conserva_los_precios_del_momento_en_que_se_agrega()
        {
            var carrito = new Carrito();
            carrito.Agregar(Producto(1, "Arroz", 10, 1, 30), Calculadora(), 1);

            var item = carrito.Obtener(1);
            Assert.Equal(13, item.Price, 6);
            Assert.Equal(13000, item.PriceBolivar, 6);
            Assert.Equal(55900, item.PricePeso, 6);
            Assert.Equal(10, item.PriceC, 6);
            Assert.Equal(10000, item.PriceCBs, 6);   // valorUnidad * dolarBolivar
            Assert.Equal(40000, item.PriceCCop, 6);  // valorUnidad * pesoDolar
        }

        [Fact]
        public void Restar_hasta_cero_quita_el_item()
        {
            var carrito = new Carrito();
            carrito.Agregar(Producto(1, "Arroz", 10, 1, 30), Calculadora(), 1);

            Assert.False(carrito.Ajustar(1, -1));
            Assert.True(carrito.Vacio);
        }

        [Fact]
        public void Rechaza_cantidades_invalidas()
        {
            var carrito = new Carrito();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                carrito.Agregar(Producto(1, "Arroz", 10, 1, 30), Calculadora(), 0));
        }

        [Fact]
        public void Los_pesos_se_redondean_por_linea_como_el_pos_web()
        {
            var carrito = new Carrito();
            // 0.12672 USD * 4000 = 506.88 -> formatPeso lo lleva a 500.
            var producto = Producto(1, "Suelto", 0.12672, 1, 0, "0");
            carrito.Agregar(producto, Calculadora(), 3);

            var item = carrito.Obtener(1);
            Assert.Equal(500, item.PricePeso, 6);
            Assert.Equal(1500, carrito.TotalPeso, 6);
        }
    }

    public class ValidadorVentaTests
    {
        private static Venta VentaValida(int despacho = Despacho.Venta)
        {
            var item = new ItemCarrito
            {
                Id = 1,
                Name = "Arroz",
                Qty = 1,
                Price = 13,
                PricePeso = 6200,
                PriceBolivar = 520,
                Mayor = "0",
                CantidadPaca = 1,
            };
            return new Venta
            {
                IdPedido = "uuid-1",
                MetodoPago = "option4",
                Despacho = despacho,
                TotalUsd = 13,
                TotalBs = 520,
                TotalCop = 6200,
                ProductosJson = Newtonsoft.Json.JsonConvert.SerializeObject(new List<ItemCarrito> { item }),
            };
        }

        [Fact]
        public void Acepta_una_venta_bien_armada()
        {
            Assert.True(ValidadorVenta.Validar(VentaValida()).Valido);
        }

        [Fact]
        public void Rechaza_venta_sin_productos()
        {
            var venta = VentaValida();
            venta.ProductosJson = "[]";
            Assert.False(ValidadorVenta.Validar(venta).Valido);
        }

        [Fact]
        public void Exige_cedula_y_nombre_en_credito()
        {
            var venta = VentaValida(Despacho.Credito);
            var resultado = ValidadorVenta.Validar(venta);
            Assert.False(resultado.Valido);
            Assert.Contains("cédula", resultado.Error);

            venta.ClienteCedula = "V1";
            venta.ClienteNombre = "Ana";
            Assert.True(ValidadorVenta.Validar(venta).Valido);
        }

        [Fact]
        public void Exige_cantidad_mayor_a_cero()
        {
            var venta = VentaValida();
            var items = venta.Productos();
            items[0].Qty = 0;
            venta.ProductosJson = Newtonsoft.Json.JsonConvert.SerializeObject(items);

            Assert.False(ValidadorVenta.Validar(venta).Valido);
        }

        [Fact]
        public void Exige_cantidadPaca_en_venta_al_mayor()
        {
            var venta = VentaValida();
            var items = venta.Productos();
            items[0].Mayor = "1";
            items[0].CantidadPaca = 0;
            venta.ProductosJson = Newtonsoft.Json.JsonConvert.SerializeObject(items);

            var resultado = ValidadorVenta.Validar(venta);
            Assert.False(resultado.Valido);
            Assert.Contains("cantidadPaca", resultado.Error);
        }

        [Fact]
        public void La_fabrica_congela_totales_y_tasas()
        {
            var calc = new CalculadoraPrecios(new ParametrosPrecios
            {
                PesoDolar = 4000,
                PesoBolivar = 0.0058,
                DolarBolivar = 1000,
                BolivarPeso = 0.0043,
                Bcv = 1000,
            });
            var carrito = new Carrito();
            carrito.Agregar(new Producto
            {
                Id = 7,
                Nombre = "Arroz",
                Origen = "v",
                PrecioCompra = 10,
                CantidadUnidades = 1,
                Porcentaje = 30,
                Mayor = "0",
            }, calc, 2);

            var sesion = new SesionLocal { UsuarioId = 15, Nombre = "Ricardo", SucursalId = 9 };
            var ahora = new DateTimeOffset(2026, 10, 18, 14, 30, 0, TimeSpan.FromHours(-4));
            var venta = VentaFabrica.Crear(carrito, "option4", Despacho.Venta,
                new Cliente { Cedula = "V1", Nombre = "Ana" }, sesion,
                new Tasas { PesoDolar = 4000, DolarBolivar = 1000 }, ahora);

            Assert.False(string.IsNullOrWhiteSpace(venta.IdPedido));
            Assert.Equal(26, venta.TotalUsd, 6);
            Assert.Equal(26000, venta.TotalBs, 6);
            Assert.Equal(111800, venta.TotalCop, 6);
            Assert.Equal("Ricardo", venta.UsuarioNombre);
            Assert.Equal(EstadoVenta.Pendiente, venta.Estado);
            Assert.True(ValidadorVenta.Validar(venta).Valido);
            Assert.Contains("peso_dolar", venta.TasasJson);
        }
    }

    public class CarritoYVentasRepositorioTests : IDisposable
    {
        private readonly string _ruta;

        public CarritoYVentasRepositorioTests()
        {
            _ruta = Path.Combine(Path.GetTempPath(), "iseller-ventas-" + Guid.NewGuid().ToString("N") + ".db");
        }

        public void Dispose()
        {
            foreach (var sufijo in new[] { "", "-wal", "-shm" })
            {
                try
                {
                    if (File.Exists(_ruta + sufijo))
                    {
                        File.Delete(_ruta + sufijo);
                    }
                }
                catch (IOException)
                {
                }
            }
        }

        private static ItemCarrito Item(int id, double qty)
        {
            return new ItemCarrito
            {
                Id = id,
                Name = "Producto " + id,
                Qty = qty,
                Price = 10,
                PricePeso = 4000,
                PriceBolivar = 1000,
                Mayor = "0",
                CantidadPaca = 1,
            };
        }

        [Fact]
        public void El_carrito_sobrevive_al_reinicio()
        {
            var repo = new CarritoRepositorio(_ruta);
            repo.GuardarItem(Item(1, 2));
            repo.GuardarItem(Item(2, 1));

            // "Reinicio": se relee todo desde disco.
            var otroRepo = new CarritoRepositorio(_ruta);
            var recargado = new Carrito();
            recargado.Cargar(otroRepo.Cargar());

            Assert.Equal(2, recargado.CantidadItems);
            Assert.Equal(2, recargado.Obtener(1).Qty);
            Assert.Equal(1, recargado.Obtener(2).Qty);

            otroRepo.Reemplazar(recargado.Items);
            Assert.Equal(2, otroRepo.Cargar().Count);

            otroRepo.Quitar(1);
            Assert.Single(otroRepo.Cargar());

            otroRepo.Vaciar();
            Assert.Empty(otroRepo.Cargar());
        }

        [Fact]
        public void Guarda_y_recupera_una_venta_pendiente()
        {
            var repo = new VentasRepositorio(_ruta);
            var venta = new Venta
            {
                IdPedido = "uuid-abc",
                FechaLocal = new DateTimeOffset(2026, 10, 18, 14, 30, 0, TimeSpan.FromHours(-4)),
                MetodoPago = "option4",
                Despacho = Despacho.Venta,
                TotalUsd = 13,
                TotalBs = 520,
                TotalCop = 6200,
                UsuarioId = 15,
                UsuarioNombre = "Ricardo",
                TasasJson = Venta.SerializarTasas(new Tasas { PesoDolar = 4000, DolarBolivar = 1000 }),
                ProductosJson = Newtonsoft.Json.JsonConvert.SerializeObject(
                    new List<ItemCarrito> { Item(1, 1) }),
                CreadaEn = DateTimeOffset.Now,
            };

            repo.Guardar(venta);

            var leida = repo.Obtener("uuid-abc");
            Assert.NotNull(leida);
            Assert.Equal(EstadoVenta.Pendiente, leida.Estado);
            Assert.Equal(13, leida.TotalUsd, 6);
            Assert.Single(leida.Productos());
            Assert.Equal(4000, Newtonsoft.Json.Linq.JObject.Parse(leida.TasasJson)["peso_dolar"].ToObject<double>(), 6);

            Assert.Single(repo.Pendientes());
            Assert.Equal(1, repo.ContarPendientes());

            var hoy = repo.Entre(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));
            Assert.Single(hoy);
        }

        [Fact]
        public void No_permite_repetir_el_mismo_id_pedido()
        {
            var repo = new VentasRepositorio(_ruta);
            var venta = new Venta { IdPedido = "uuid-repetido", MetodoPago = "option4", Despacho = 1, CreadaEn = DateTimeOffset.Now };
            repo.Guardar(venta);

            venta.TotalUsd = 99;
            repo.Guardar(venta);

            Assert.Single(repo.Pendientes());
            Assert.Equal(99, repo.Obtener("uuid-repetido").TotalUsd, 6);
        }

        [Fact]
        public void Pasa_de_pendiente_a_enviada_y_registra_fallos()
        {
            var repo = new VentasRepositorio(_ruta);
            repo.Guardar(new Venta
            {
                IdPedido = "uuid-sync",
                MetodoPago = "option4",
                Despacho = 1,
                CreadaEn = DateTimeOffset.Now,
            });

            Assert.Equal(1, repo.ContarPendientes());

            repo.RegistrarFallo("uuid-sync", "sin red");
            var pendiente = repo.Obtener("uuid-sync");
            Assert.Equal(EstadoVenta.Pendiente, pendiente.Estado);
            Assert.Equal(1, pendiente.Intentos);
            Assert.Equal("sin red", pendiente.UltimoError);

            repo.MarcarEnviada("uuid-sync", DateTimeOffset.Now);
            var enviada = repo.Obtener("uuid-sync");
            Assert.Equal(EstadoVenta.Enviada, enviada.Estado);
            Assert.Null(enviada.UltimoError);
            Assert.NotNull(enviada.EnviadaEn);
            Assert.Equal(0, repo.ContarPendientes());
            Assert.Single(repo.Enviadas(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1)));
        }

        [Fact]
        public void Enviadas_se_filtra_por_cajero()
        {
            var repo = new VentasRepositorio(_ruta);
            foreach (int usuario in new[] { 22, 23 })
            {
                repo.Guardar(new Venta
                {
                    IdPedido = "uuid-cajero-" + usuario,
                    MetodoPago = "option4",
                    Despacho = 1,
                    UsuarioId = usuario,
                    UsuarioNombre = "CAJA NRO " + usuario,
                    CreadaEn = DateTimeOffset.Now,
                });
                repo.MarcarEnviada("uuid-cajero-" + usuario, DateTimeOffset.Now);
            }

            var desde = DateTimeOffset.Now.AddDays(-1);
            var hasta = DateTimeOffset.Now.AddDays(1);

            Assert.Equal(2, repo.Enviadas(desde, hasta).Count);
            var solo22 = repo.Enviadas(desde, hasta, 22);
            Assert.Single(solo22);
            Assert.Equal(22, solo22[0].UsuarioId);
        }
    }

    public class TicketEscPosTests
    {
        private static Venta VentaDePrueba(string metodo = "option4")
        {
            var items = new List<ItemCarrito>
            {
                new ItemCarrito { Id = 1, Name = "Harina Pan", Qty = 2, Price = 1.67, PricePeso = 7000, PriceBolivar = 1674, Mayor = "0", CantidadPaca = 1 },
                new ItemCarrito { Id = 2, Name = "Coca Cola 2 L", Qty = 1, Price = 1.5, PricePeso = 6200, PriceBolivar = 1500, Mayor = "0", CantidadPaca = 1 },
            };
            return new Venta
            {
                IdPedido = "uuid-ticket",
                FechaLocal = new DateTimeOffset(2026, 10, 18, 14, 30, 0, TimeSpan.FromHours(-4)),
                MetodoPago = metodo,
                Despacho = 1,
                ProductosJson = Newtonsoft.Json.JsonConvert.SerializeObject(items),
            };
        }

        private static string Texto(byte[] bytes)
        {
            return System.Text.Encoding.ASCII.GetString(bytes);
        }

        [Fact]
        public void El_ancho_de_linea_depende_del_papel()
        {
            var lineas58 = TicketEscPos.Componer(VentaDePrueba(), "YOLA MARKET 1",
                new OpcionesImpresion { AnchoMm = 58 });
            var lineas80 = TicketEscPos.Componer(VentaDePrueba(), "YOLA MARKET 1",
                new OpcionesImpresion { AnchoMm = 80 });

            Assert.All(lineas58, l => Assert.True(l.Texto.Length <= 32, "58 mm: " + l.Texto));
            Assert.All(lineas80, l => Assert.True(l.Texto.Length <= 48, "80 mm: " + l.Texto));

            Assert.Equal(32, lineas58[3].Texto.Length); // separador
            Assert.Equal(48, lineas80[3].Texto.Length);
        }

        private static byte[] Impresion58()
        {
            return TicketEscPos.Generar(VentaDePrueba(), "YOLA MARKET 1",
                new OpcionesImpresion { AnchoMm = 58, AbrirCajon = true });
        }

        private static byte[] Impresion80()
        {
            return TicketEscPos.Generar(VentaDePrueba(), "YOLA MARKET 1",
                new OpcionesImpresion { AnchoMm = 80, AbrirCajon = true });
        }

        [Fact]
        public void Lleva_sucursal_productos_y_total()
        {
            var texto = Texto(Impresion58());
            Assert.Contains("YOLA MARKET 1", texto);
            Assert.Contains("Harina Pan", texto);
            Assert.Contains("TOTAL:", texto);
            Assert.Contains("4.848,00 Bs", texto); // 1.674*2 + 1.500
        }

        [Fact]
        public void Usa_la_moneda_del_metodo_de_pago()
        {
            // option6 = pesos: 7.000*2 + 6.200, redondeado a centenas y sin decimales.
            Assert.Contains("20.200 COP", Texto(TicketEscPos.Generar(VentaDePrueba("option6"), "X", new OpcionesImpresion())));
            // option5 = dólares: 1,67*2 + 1,50.
            Assert.Contains("4,84 USD", Texto(TicketEscPos.Generar(VentaDePrueba("option5"), "X", new OpcionesImpresion())));
        }

        [Fact]
        public void Solo_intenta_abrir_el_cajon_con_efectivo()
        {
            byte[] conEfectivo = TicketEscPos.Generar(VentaDePrueba("option4"), "X", new OpcionesImpresion { AbrirCajon = true });
            byte[] conTransferencia = TicketEscPos.Generar(VentaDePrueba("option3"), "X", new OpcionesImpresion { AbrirCajon = true });
            byte[] cajonApagado = TicketEscPos.Generar(VentaDePrueba("option4"), "X", new OpcionesImpresion { AbrirCajon = false });

            Assert.True(Contiene(conEfectivo, new byte[] { 0x1B, 0x70 }));
            Assert.False(Contiene(conTransferencia, new byte[] { 0x1B, 0x70 }));
            Assert.False(Contiene(cajonApagado, new byte[] { 0x1B, 0x70 }));
        }

        [Fact]
        public void Corta_el_papel_al_final()
        {
            Assert.True(Contiene(Impresion58(), new byte[] { 0x1D, 0x56 }));
        }

        [Fact]
        public void Translitera_acentos_a_ascii()
        {
            var venta = VentaDePrueba();
            var items = venta.Productos();
            items[0].Name = "Jamón Ñandú";
            venta.ProductosJson = Newtonsoft.Json.JsonConvert.SerializeObject(items);

            var texto = Texto(TicketEscPos.Generar(venta, "Sucursal", new OpcionesImpresion()));
            Assert.Contains("Jamon Nandu", texto);
        }

        private static bool Contiene(byte[] fuente, byte[] buscado)
        {
            for (int i = 0; i <= fuente.Length - buscado.Length; i++)
            {
                bool igual = true;
                for (int j = 0; j < buscado.Length; j++)
                {
                    if (fuente[i + j] != buscado[j])
                    {
                        igual = false;
                        break;
                    }
                }
                if (igual)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
