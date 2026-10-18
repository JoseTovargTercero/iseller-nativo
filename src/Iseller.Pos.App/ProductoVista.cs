using System;
using System.Globalization;
using Iseller.Pos.Core.Precios;

namespace Iseller.Pos.App
{
    /// <summary>Producto del catálogo con los precios ya calculados para la vista.</summary>
    public sealed class ProductoVista
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public double Stock { get; set; }
        public bool EsCombo { get; set; }
        public string PrecioDolar { get; set; }
        public string PrecioBs { get; set; }
        public string PrecioPeso { get; set; }

        /// <summary>Producto original, para agregarlo al carrito.</summary>
        public Core.Modelos.Producto Modelo { get; set; }

        public static ProductoVista Desde(Core.Modelos.Producto p, CalculadoraPrecios calculadora)
        {
            var precios = calculadora.Calcular(new ProductoPrecio
            {
                CantidadUnidades = p.CantidadUnidades,
                Origen = p.Origen,
                PrecioCompra = p.PrecioCompra,
                Porcentaje = p.Porcentaje,
                Mayor = p.Mayor,
            });

            return new ProductoVista
            {
                Id = p.Id,
                Nombre = p.EsCombo == 1 ? p.Nombre + "  [Combo]" : p.Nombre,
                Stock = p.Stock,
                EsCombo = p.EsCombo == 1,
                Modelo = p,
                PrecioDolar = "$" + Numero(precios.PrecioVentaDolar),
                PrecioBs = Numero(precios.PrecioVentaBs) + " Bs",
                PrecioPeso = Numero(precios.PrecioVentaPeso) + " Cop",
            };
        }

        private static string Numero(double valor)
        {
            return valor.ToString("N2", CultureInfo.GetCultureInfo("es-VE"));
        }
    }
}
