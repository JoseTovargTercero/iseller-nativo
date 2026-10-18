using System;
using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.Core.Auth
{
    /// <summary>Reglas de vigencia decididas con el dueño (PLAN.md §7).</summary>
    public static class ReglaSesion
    {
        /// <summary>Días que un cajero puede entrar sin haber conectado.</summary>
        public static readonly TimeSpan LimiteOffline = TimeSpan.FromDays(2);

        /// <summary>Horas que se puede vender con una tasa sin actualizar.</summary>
        public static readonly TimeSpan LimiteTasaVieja = TimeSpan.FromHours(48);

        public static bool PuedeEntrarOffline(SesionLocal sesion, DateTimeOffset ahora)
        {
            return sesion != null && (ahora - sesion.UltimoLoginValidoEn) <= LimiteOffline;
        }

        public static TimeSpan? AntiguedadTasa(Tasas tasas, DateTimeOffset ahora)
        {
            return tasas != null && tasas.ActualizadoEn.HasValue
                ? ahora - tasas.ActualizadoEn.Value
                : (TimeSpan?)null;
        }

        public static bool TasaVencida(Tasas tasas, DateTimeOffset ahora)
        {
            var antiguedad = AntiguedadTasa(tasas, ahora);
            return !antiguedad.HasValue || antiguedad.Value > LimiteTasaVieja;
        }
    }
}
