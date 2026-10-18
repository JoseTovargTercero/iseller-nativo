using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.Core.Caja
{
    /// <summary>Cuándo se exige abrir/cerrar caja, igual que el POS web.</summary>
    public static class CajaReglas
    {
        /// <summary>La web solo obliga a los cajeros (nivel 2) y si cortes_caja está activo.</summary>
        public static bool Requiere(ConfiguracionNegocio configuracion, int nivel)
        {
            return configuracion != null && configuracion.CortesCaja == 1 && nivel == 2;
        }

        public static bool BloqueaVenta(EstadoCaja estado)
        {
            return estado != EstadoCaja.Abierta;
        }

        public static string Descripcion(EstadoCaja estado)
        {
            switch (estado)
            {
                case EstadoCaja.Abierta: return "Caja abierta";
                case EstadoCaja.Cerrada: return "Caja cerrada";
                default: return "Caja sin apertura";
            }
        }
    }
}
