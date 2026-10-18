using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Iseller.Pos.Core.Modelos
{
    /// <summary>
    /// Item del carrito con los precios congelados al momento de agregarlo,
    /// igual que hace el POS web. Los nombres JSON coinciden con lo que espera
    /// agregarAlCarrito() en el backend.
    /// </summary>
    public sealed class ItemCarrito
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("price_C")] public double PriceC { get; set; }
        [JsonProperty("price_C_Bs")] public double PriceCBs { get; set; }
        [JsonProperty("price_C_Cop")] public double PriceCCop { get; set; }
        [JsonProperty("price")] public double Price { get; set; }
        [JsonProperty("pricePeso")] public double PricePeso { get; set; }
        [JsonProperty("priceBolivar")] public double PriceBolivar { get; set; }
        [JsonProperty("qty")] public double Qty { get; set; }
        [JsonProperty("mayor")] public string Mayor { get; set; }
        [JsonProperty("cantidadPaca")] public double CantidadPaca { get; set; }
        [JsonProperty("es_combo")] public int EsCombo { get; set; }
        [JsonProperty("combo_items")] public List<ItemCombo> ComboItems { get; set; }

        public ItemCarrito()
        {
            Name = "";
            Mayor = "0";
            CantidadPaca = 1;
            ComboItems = new List<ItemCombo>();
        }

        [JsonIgnore]
        public double SubtotalUsd
        {
            get { return Price * Qty; }
        }

        [JsonIgnore]
        public double SubtotalBs
        {
            get { return PriceBolivar * Qty; }
        }

        /// <summary>Igual que el POS web: los pesos se redondean por línea.</summary>
        [JsonIgnore]
        public double SubtotalPeso
        {
            get { return Math.Round(PricePeso * Qty); }
        }
    }
}
