using System.Collections.Generic;

namespace Iseller.Pos.Core.Modelos
{
    public sealed class ItemCombo
    {
        public string Nombre { get; set; }
        public double Cantidad { get; set; }
    }

    public sealed class Producto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string CodigoBarras { get; set; }
        public string Origen { get; set; }
        public double PrecioCompra { get; set; }
        public double CantidadUnidades { get; set; }
        public double Porcentaje { get; set; }
        public string Mayor { get; set; }
        public int EsCombo { get; set; }
        public string Foto { get; set; }
        public double Stock { get; set; }
        public List<ItemCombo> ComboItems { get; set; }

        public Producto()
        {
            Origen = "v";
            Mayor = "0";
            CodigoBarras = "";
            Nombre = "";
            Foto = "";
            ComboItems = new List<ItemCombo>();
        }
    }

    public sealed class Cliente
    {
        public string Cedula { get; set; }
        public string Nombre { get; set; }
        public string Telefono { get; set; }
    }

    public sealed class ConfiguracionNegocio
    {
        public int Tickets { get; set; }
        public int BsTicket { get; set; }
        public int CortesCaja { get; set; }
        public int RegistroClientes { get; set; }
    }

    public sealed class Tasas
    {
        public double PesoDolar { get; set; }
        public double PesoBolivar { get; set; }
        public double DolarBolivar { get; set; }
        public double BolivarPeso { get; set; }
        public double Bcv { get; set; }

        /// <summary>JSON tal cual lo manda el negocio (las claves deciden que precio mostrar).</summary>
        public string DataMonedasJson { get; set; }

        public bool PrecioDolarVisible { get; set; }
        public bool PrecioPesoVisible { get; set; }

        /// <summary>Hora del servidor cuando se descargaron. Null si nunca.</summary>
        public System.DateTimeOffset? ActualizadoEn { get; set; }

        public Tasas()
        {
            DataMonedasJson = "{}";
        }
    }
}
