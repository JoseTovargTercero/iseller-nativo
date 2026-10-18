using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Iseller.Pos.Core.Modelos
{
    /// <summary>Carrito apartado para retomarlo más tarde.</summary>
    public sealed class Reservado
    {
        public string Id { get; set; }
        public string Cliente { get; set; }
        public string ProductosJson { get; set; }
        public DateTimeOffset CreadaEn { get; set; }

        public Reservado()
        {
            Id = "";
            Cliente = "";
            ProductosJson = "[]";
        }

        public List<ItemCarrito> Productos()
        {
            if (string.IsNullOrWhiteSpace(ProductosJson))
            {
                return new List<ItemCarrito>();
            }
            try
            {
                return JsonConvert.DeserializeObject<List<ItemCarrito>>(ProductosJson)
                       ?? new List<ItemCarrito>();
            }
            catch (JsonException)
            {
                return new List<ItemCarrito>();
            }
        }

        public int CantidadProductos
        {
            get { return Productos().Count; }
        }

        public double TotalUsd
        {
            get
            {
                double total = 0;
                foreach (var item in Productos())
                {
                    total += item.Price * item.Qty;
                }
                return total;
            }
        }

        public double TotalBs
        {
            get
            {
                double total = 0;
                foreach (var item in Productos())
                {
                    total += item.PriceBolivar * item.Qty;
                }
                return total;
            }
        }

        public double TotalPeso
        {
            get
            {
                double total = 0;
                foreach (var item in Productos())
                {
                    total += Math.Round(item.PricePeso * item.Qty);
                }
                return Math.Round(total);
            }
        }
    }
}
