namespace Iseller.Pos.App
{
    /// <summary>Opción del filtro de cajeros en la pestaña "No guardados".</summary>
    public sealed class CajeroOpcion
    {
        /// <summary>Null = todos los cajeros.</summary>
        public int? Id { get; set; }
        public string Nombre { get; set; }

        public CajeroOpcion()
        {
            Nombre = "";
        }
    }
}
