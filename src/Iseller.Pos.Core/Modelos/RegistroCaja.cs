using System;
using System.Collections.Generic;

namespace Iseller.Pos.Core.Modelos
{
    public enum EstadoCaja
    {
        SinApertura,
        Abierta,
        Cerrada,
    }

    /// <summary>Apertura o cierre de caja guardado en la terminal.</summary>
    public sealed class RegistroCaja
    {
        public string Id { get; set; }

        /// <summary>"apertura" o "cierre".</summary>
        public string Tipo { get; set; }

        public DateTimeOffset CreadaEn { get; set; }
        public string Observaciones { get; set; }
        public int UsuarioId { get; set; }
        public DateTimeOffset? SincronizadaEn { get; set; }
        public Dictionary<string, double> Montos { get; set; }

        public RegistroCaja()
        {
            Id = "";
            Tipo = "apertura";
            Observaciones = "";
            Montos = new Dictionary<string, double>();
        }

        public double Monto(string clave)
        {
            double valor;
            return Montos != null && Montos.TryGetValue(clave, out valor) ? valor : 0;
        }

        public string FechaLocal
        {
            get { return CreadaEn.ToString("yyyy-MM-dd"); }
        }

        public bool EsCierre
        {
            get { return Tipo == "cierre"; }
        }
    }
}
