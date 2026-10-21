using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Iseller.Pos.Core.Modelos
{
    /// <summary>
    /// Venta guardada en la caja. Lleva los precios y las tasas congelados,
    /// así que el total que vio el cajero es el que se sincroniza después.
    /// </summary>
    public sealed class Venta
    {
        [JsonProperty("id_pedido")] public string IdPedido { get; set; }
        [JsonProperty("fecha_local")] public DateTimeOffset FechaLocal { get; set; }
        [JsonProperty("metodo_pago")] public string MetodoPago { get; set; }
        [JsonProperty("despacho")] public int Despacho { get; set; }
        [JsonProperty("total_usd")] public double TotalUsd { get; set; }
        [JsonProperty("total_bs")] public double TotalBs { get; set; }
        [JsonProperty("total_cop")] public double TotalCop { get; set; }
        [JsonProperty("cliente_cedula")] public string ClienteCedula { get; set; }
        [JsonProperty("cliente_nombre")] public string ClienteNombre { get; set; }
        [JsonProperty("cliente_telefono")] public string ClienteTelefono { get; set; }
        [JsonProperty("usuario_id")] public int UsuarioId { get; set; }
        [JsonProperty("usuario_nombre")] public string UsuarioNombre { get; set; }
        [JsonProperty("estado")] public string Estado { get; set; }
        [JsonProperty("intentos")] public int Intentos { get; set; }
        [JsonProperty("ultimo_error")] public string UltimoError { get; set; }
        [JsonProperty("tasas_json")] public string TasasJson { get; set; }
        [JsonProperty("productos_json")] public string ProductosJson { get; set; }
        /// <summary>Desglose del pago fraccionado (option8), o null si no lo es.</summary>
        [JsonProperty("pagos_json")] public string PagosJson { get; set; }
        [JsonProperty("creada_en")] public DateTimeOffset CreadaEn { get; set; }
        [JsonProperty("enviada_en")] public DateTimeOffset? EnviadaEn { get; set; }

        public Venta()
        {
            IdPedido = "";
            MetodoPago = "";
            ClienteCedula = "";
            ClienteNombre = "";
            ClienteTelefono = "";
            UsuarioNombre = "";
            Estado = EstadoVenta.Pendiente;
            TasasJson = "{}";
            ProductosJson = "[]";
        }

        /// <summary>Productos del carrito tal como se enviarán al servidor.</summary>
        public List<ItemCarrito> Productos()
        {
            if (string.IsNullOrWhiteSpace(ProductosJson))
            {
                return new List<ItemCarrito>();
            }
            return JsonConvert.DeserializeObject<List<ItemCarrito>>(ProductosJson) ?? new List<ItemCarrito>();
        }

        /// <summary>Desglose del pago fraccionado, o null si la venta no lo es.</summary>
        public Dictionary<string, double> PagosFraccionados()
        {
            if (string.IsNullOrWhiteSpace(PagosJson))
            {
                return null;
            }
            return JsonConvert.DeserializeObject<Dictionary<string, double>>(PagosJson);
        }

        /// <summary>Tasas usadas, en el mismo formato del contrato.</summary>
        public static string SerializarTasas(Tasas tasas)
        {
            if (tasas == null)
            {
                return "{}";
            }

            JObject monedas;
            try
            {
                monedas = string.IsNullOrWhiteSpace(tasas.DataMonedasJson)
                    ? new JObject()
                    : JObject.Parse(tasas.DataMonedasJson);
            }
            catch (JsonException)
            {
                monedas = new JObject();
            }

            return new JObject
            {
                ["peso_dolar"] = tasas.PesoDolar,
                ["peso_bolivar"] = tasas.PesoBolivar,
                ["dolar_bolivar"] = tasas.DolarBolivar,
                ["bolivar_peso"] = tasas.BolivarPeso,
                ["bcv"] = tasas.Bcv,
                ["data_monedas"] = monedas,
            }.ToString(Formatting.None);
        }
    }
}
