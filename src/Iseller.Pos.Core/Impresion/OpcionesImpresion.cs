namespace Iseller.Pos.Core.Impresion
{
    /// <summary>Configuración de la impresora de la caja.</summary>
    public sealed class OpcionesImpresion
    {
        public bool ImprimirTickets { get; set; }
        public string Impresora { get; set; }

        /// <summary>58 u 80 mm de papel.</summary>
        public int AnchoMm { get; set; }

        /// <summary>Abrir el cajón monedero al cobrar en efectivo (si existe).</summary>
        public bool AbrirCajon { get; set; }

        public OpcionesImpresion()
        {
            ImprimirTickets = true;
            Impresora = "";
            AnchoMm = 58;
            AbrirCajon = true;
        }

        public int Columnas
        {
            get { return AnchoMm >= 80 ? 48 : 32; }
        }
    }
}
