using System;

namespace Iseller.Pos.Core.Modelos
{
    /// <summary>Motivos por los que una venta puede quedar sin enviar.</summary>
    public static class EstadoVenta
    {
        public const string Pendiente = "pendiente";
        public const string Enviada = "enviada";
        public const string Error = "error";
    }
}
