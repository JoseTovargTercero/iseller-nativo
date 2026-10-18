namespace Iseller.Pos.Core.Precios
{
    /// <summary>Campos de productos que necesita el calculo de precios.</summary>
    public sealed class ProductoPrecio
    {
        public double CantidadUnidades { get; set; }
        public string Origen { get; set; }
        public double PrecioCompra { get; set; }
        public double Porcentaje { get; set; }
        public string Mayor { get; set; }
    }
}
