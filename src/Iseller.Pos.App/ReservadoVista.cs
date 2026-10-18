using System;
using System.Globalization;
using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.App
{
    public sealed class ReservadoVista
    {
        private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-VE");

        public Reservado Reservado { get; set; }

        public string Fecha
        {
            get { return Reservado.CreadaEn.ToLocalTime().ToString("dd/MM HH:mm", Formato); }
        }

        public string Cliente
        {
            get { return Reservado.Cliente; }
        }

        public string Productos
        {
            get { return Reservado.CantidadProductos + " producto(s)"; }
        }

        public string Pesos
        {
            get { return Reservado.TotalPeso.ToString("N0", Formato) + " Cop"; }
        }

        public string Bolivares
        {
            get { return Reservado.TotalBs.ToString("N2", Formato) + " Bs"; }
        }

        public string Dolares
        {
            get { return "$" + Reservado.TotalUsd.ToString("N2", Formato); }
        }
    }
}
