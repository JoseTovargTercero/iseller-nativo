using System;

namespace Iseller.Pos.Core.Precios
{
    /// <summary>
    /// Port exacto de configurar/_calculadrora_precios.php::calcularPrecios().
    /// Cualquier cambio aqui debe replicarse en el PHP y viceversa.
    /// Los tests de Iseller.Pos.Tests comparan contra valores generados por el PHP real.
    /// </summary>
    public sealed class CalculadoraPrecios
    {
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

            // En PHP el || tiene menor precedencia que ?: y se evalua igual que aqui.
            double valorPesos = (_parametros.PrecioPesoVisible || origen == "c")
                ? precioPesoVenta
                : precioBolivarPeso;

            double precioDolar = _parametros.PrecioDolarVisible
                ? Redondear2(precioDolarVenta)
                : Redondear2(precioBolivarDolar);

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

        /// <summary>Equivalente a round($valor, 2) de PHP.</summary>
        private static double Redondear2(double valor)
        {
            return Math.Round(valor, 2, MidpointRounding.AwayFromZero);
        }
    }
}
