namespace Iseller.Pos.Core.Precios
{
    /// <summary>
    /// Tasas y banderas que en el PHP vienen de configurar/_tasas_cambio.php ($data_monedas).
    /// Se guardan con la venta para poder reproducir el precio exacto que se cobro.
    /// </summary>
    public sealed class ParametrosPrecios
    {
        public double PesoDolar { get; set; }
        public double PesoBolivar { get; set; }
        public double DolarBolivar { get; set; }
        public double BolivarPeso { get; set; }
        public double Bcv { get; set; }

        /// <summary>Equivale a isset($data_monedas['precio_dolar_visible']).</summary>
        public bool PrecioDolarVisible { get; set; }

        /// <summary>Equivale a isset($data_monedas['precio_peso_visible']).</summary>
        public bool PrecioPesoVisible { get; set; }
    }
}
