using System;
using System.Collections.Generic;
using System.Globalization;
using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.App
{
    /// <summary>Venta pendiente con lo necesario para listarla.</summary>
    public sealed class VentaVista
    {
        private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-VE");

        public Venta Venta { get; set; }

        public string Fecha
        {
            get { return Venta.CreadaEn.ToLocalTime().ToString("dd/MM HH:mm", Formato); }
        }

        public string Tipo
        {
            get
            {
                switch (Venta.Despacho)
                {
                    case 2: return "Crédito";
                    case 3: return "Descuento";
                    default: return "Venta";
                }
            }
        }

        public string Metodo
        {
            get
            {
                switch (Venta.MetodoPago)
                {
                    case "option1": return "Punto";
                    case "option2": return "Pago Móvil";
                    case "option3": return "Transferencia";
                    case "option4": return "Efectivo";
                    case "option5": return "Dólares";
                    case "option6": return "Pesos";
                    case "option7": return "BioPago";
                    default: return "—";
                }
            }
        }

        public string Dolares
        {
            get { return "$" + Venta.TotalUsd.ToString("N2", Formato); }
        }

        public string Bolivares
        {
            get { return Venta.TotalBs.ToString("N2", Formato) + " Bs"; }
        }

        public string Pesos
        {
            get { return Venta.TotalCop.ToString("N0", Formato) + " Cop"; }
        }
    }
}
