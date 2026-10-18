using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Iseller.Pos.Core.Precios
{
    /// <summary>
    /// Port exacto de configurar/_calculadrora_precios.php::calcularPrecios().
    /// Cualquier cambio aqui debe replicarse en el PHP y viceversa.
    /// Los tests de Iseller.Pos.Tests comparan contra valores generados por el PHP real.
    /// </summary>
    public sealed class CalculadoraPrecios
    {
        private static readonly Regex NumeroInicial = new Regex(
            @"^[ \t\n\r\v\f]*[+-]?(\d+(\.\d*)?|\.\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly ParametrosPrecios _parametros;

        public CalculadoraPrecios(ParametrosPrecios parametros)
        {
            _parametros = parametros ?? throw new ArgumentNullException(nameof(parametros));
        }

        public PreciosCalculados Calcular(ProductoPrecio producto)
        {
            if (producto == null) throw new ArgumentNullException(nameof(producto));

            double cantidadUnidad = producto.CantidadUnidades;
            string origen = producto.Origen;
            double precioCompra = producto.PrecioCompra;
            double porcentaje = producto.Porcentaje;
            double cantidadPorPrecio = producto.Mayor == "1" ? 1 : cantidadUnidad;

            // Precio en dolares por unidad (o por paca si es al mayor).
            double precioDolarCompra = precioCompra / cantidadPorPrecio;
            double precioDolarVenta = precioDolarCompra + (precioDolarCompra * porcentaje / 100);

            // PHP: number_format($precioDolarVenta, 2, '.', ',') y luego (float).
            string precioDolarVisible = NumeroFormateado(precioDolarVenta);

            // Dolar -> pesos, con redondeo a centenas.
            double precioPesoVenta = FormatPeso(precioDolarVenta * _parametros.PesoDolar);

            // Bolivares segun el origen del producto.
            double precioBsVenta;
            if (origen == "c")
            {
                precioBsVenta = (precioPesoVenta / _parametros.PesoBolivar) / 1000;
            }
            else
            {
                precioBsVenta = precioDolarVenta * _parametros.DolarBolivar;
            }

            double precioBolivarPeso = FormatPeso((precioBsVenta * _parametros.BolivarPeso) * 1000);
            double precioBolivarDolar = precioBsVenta / _parametros.Bcv;

            // Ojo: en PHP el || tiene menor precedencia que ?: y se evalua igual que aqui.
            double valorPesos = (_parametros.PrecioPesoVisible || origen == "c")
                ? precioPesoVenta
                : precioBolivarPeso;

            double precioDolar = _parametros.PrecioDolarVisible
                ? FloatDesdeTextoPhp(precioDolarVisible)
                : FloatDesdeTextoPhp(NumeroFormateado(precioBolivarDolar));

            return new PreciosCalculados
            {
                PrecioVentaDolar = precioDolar,
                PrecioVentaBs = precioBsVenta,
                PrecioVentaPeso = valorPesos
            };
        }

        /// <summary>Port de formatPeso(): minimo 100 y redondeo a la centena mas cercana.</summary>
        private static double FormatPeso(double valor)
        {
            if (valor < 100)
            {
                return 100;
            }

            // PHP convierte a int antes del %, por eso se trunca.
            long residuo = (long)valor % 100;

            if (residuo >= 50)
            {
                return Math.Ceiling(valor / 100) * 100;
            }

            return Math.Floor(valor / 100) * 100;
        }

        /// <summary>Equivalente a number_format($valor, 2, '.', ',') de PHP.</summary>
        private static string NumeroFormateado(double valor)
        {
            double redondeado = Math.Round(valor, 2, MidpointRounding.AwayFromZero);
            return redondeado.ToString("N2", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Equivalente a (float)"1,234.56" en PHP: toma solo el prefijo numerico.
        /// Esto reproduce el bug del PHP actual con precios >= 1000 (devuelve 1 en vez de 1234.56).
        /// </summary>
        private static double FloatDesdeTextoPhp(string texto)
        {
            if (string.IsNullOrEmpty(texto))
            {
                return 0;
            }

            Match match = NumeroInicial.Match(texto);
            if (!match.Success)
            {
                return 0;
            }

            double valor;
            return double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out valor)
                ? valor
                : 0;
        }
    }
}
