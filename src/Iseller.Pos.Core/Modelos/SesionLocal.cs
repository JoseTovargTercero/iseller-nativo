using System;
using System.Collections.Generic;

namespace Iseller.Pos.Core.Modelos
{
    /// <summary>Sesión guardada en la caja para entrar sin conexión.</summary>
    public sealed class SesionLocal
    {
        public string Token { get; set; }
        public int UsuarioId { get; set; }
        public string Nombre { get; set; }
        public string Usuario { get; set; }
        public int Nivel { get; set; }
        public int BssId { get; set; }
        public int SucursalId { get; set; }
        public string SucursalNombre { get; set; }
        public List<int> Permisos { get; set; }
        public bool PuedeVenderAlMayor { get; set; }
        public ConfiguracionNegocio Configuracion { get; set; }

        /// <summary>Vencimiento del token en el servidor (segundos que informó el login).</summary>
        public int ExpiraEnSegundos { get; set; }

        /// <summary>Última vez que el servidor confirmó el login. Base del límite offline (2 días).</summary>
        public DateTimeOffset UltimoLoginValidoEn { get; set; }

        // Desbloqueo offline: PBKDF2 de la clave del cajero.
        public string ClaveSal { get; set; }
        public string ClaveHash { get; set; }
        public int ClaveIteraciones { get; set; }

        public SesionLocal()
        {
            Permisos = new List<int>();
            Configuracion = new ConfiguracionNegocio();
            Nombre = "";
            Usuario = "";
            SucursalNombre = "";
        }
    }
}
