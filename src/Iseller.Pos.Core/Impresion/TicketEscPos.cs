using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.Core.Impresion
{
    /// <summary>Una línea del ticket, antes de convertirla a ESC/POS.</summary>
    public sealed class LineaTicket
    {
        public string Texto { get; private set; }
        public bool Centrada { get; private set; }
        public bool Negrita { get; private set; }

        public LineaTicket(string texto, bool centrada = false, bool negrita = false)
        {
            Texto = texto ?? "";
            Centrada = centrada;
            Negrita = negrita;
        }
    }

    /// <summary>
    /// Arma el ticket. La composición del texto está separada de los comandos ESC/POS
    /// para poder probar el ancho de línea sin lidiar con los bytes de control.
    /// El texto se translitera a ASCII para que salga bien en impresoras baratas.
    /// </summary>
    public static class TicketEscPos
    {
        private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-VE");

        private static readonly byte[] Inicializar = { 0x1B, 0x40 };            // ESC @
        private static readonly byte[] NegritaOn = { 0x1B, 0x45, 0x01 };        // ESC E 1
        private static readonly byte[] NegritaOff = { 0x1B, 0x45, 0x00 };       // ESC E 0
        private static readonly byte[] Centrar = { 0x1B, 0x61, 0x01 };          // ESC a 1
        private static readonly byte[] Izquierda = { 0x1B, 0x61, 0x00 };        // ESC a 0
        private static readonly byte[] Cortar = { 0x1D, 0x56, 0x42, 0x00 };     // GS V 66 0
        private static readonly byte[] Avanzar = { 0x0A, 0x0A, 0x0A };
        private static readonly byte[] AbrirCajon = { 0x1B, 0x70, 0x00, 0x19, 0xFA }; // ESC p 0 25 250

        /// <summary>Texto del ticket, ya ajustado al ancho del papel.</summary>
        public static List<LineaTicket> Componer(Venta venta, string sucursalNombre, OpcionesImpresion opciones)
        {
            if (venta == null)
            {
                throw new ArgumentNullException(nameof(venta));
            }

            int ancho = (opciones ?? new OpcionesImpresion()).Columnas;
            string moneda = MonedaDe(venta.MetodoPago);
            var lineas = new List<LineaTicket>();

            lineas.Add(new LineaTicket(sucursalNombre ?? "", true, true));
            lineas.Add(new LineaTicket(venta.FechaLocal.ToString("dd/MM/yyyy hh:mm tt", Formato), true));
            lineas.Add(new LineaTicket("* Nota de entrega", true));
            lineas.Add(new LineaTicket(new string('-', ancho)));
            lineas.Add(new LineaTicket(Cabecera(ancho)));
            lineas.Add(new LineaTicket(new string('-', ancho)));

            int numero = 1;
            double total = 0;
            foreach (var item in venta.Productos())
            {
                double monto = MontoDe(item, moneda);
                total += monto;
                lineas.Add(new LineaTicket(LineaItem(numero, item, monto, moneda, ancho)));
                numero++;
            }

            lineas.Add(new LineaTicket(new string('-', ancho)));
            lineas.Add(new LineaTicket(Derecha("TOTAL: " + Numero(total) + " " + moneda, ancho), false, true));

            if (!string.IsNullOrWhiteSpace(venta.ClienteNombre) || !string.IsNullOrWhiteSpace(venta.ClienteCedula))
            {
                lineas.Add(new LineaTicket(Ajustar("Cliente: " + (venta.ClienteCedula ?? "") + " " + (venta.ClienteNombre ?? ""), ancho)));
            }

            lineas.Add(new LineaTicket("Pago: " + NombreMetodo(venta.MetodoPago)));
            lineas.Add(new LineaTicket(Ajustar(venta.UsuarioNombre ?? "", ancho)));
            lineas.Add(new LineaTicket(new string('-', ancho)));
            lineas.Add(new LineaTicket("GRACIAS POR SU COMPRA!", true));

            return lineas;
        }

        /// <summary>Ticket listo para mandar a la impresora.</summary>
        public static byte[] Generar(Venta venta, string sucursalNombre, OpcionesImpresion opciones)
        {
            var salida = new List<byte>();
            salida.AddRange(Inicializar);

            foreach (var linea in Componer(venta, sucursalNombre, opciones))
            {
                salida.AddRange(linea.Centrada ? Centrar : Izquierda);
                if (linea.Negrita)
                {
                    salida.AddRange(NegritaOn);
                }
                salida.AddRange(Texto(linea.Texto));
                salida.Add(0x0A);
                if (linea.Negrita)
                {
                    salida.AddRange(NegritaOff);
                }
            }

            salida.AddRange(Avanzar);
            salida.AddRange(Cortar);

            var opcionesEfectivas = opciones ?? new OpcionesImpresion();
            if (opcionesEfectivas.AbrirCajon && EsEfectivo(venta.MetodoPago))
            {
                salida.AddRange(AbrirCajon);
            }

            return salida.ToArray();
        }

        /// <summary>Comando para abrir el cajón monedero sin imprimir nada.</summary>
        public static byte[] ComandoCajon()
        {
            return (byte[])AbrirCajon.Clone();
        }

        public static bool EsEfectivo(string metodoPago)
        {
            return metodoPago == "option4";
        }

        public static string MonedaDe(string metodoPago)
        {
            if (metodoPago == "option5")
            {
                return "USD";
            }
            if (metodoPago == "option6")
            {
                return "COP";
            }
            return "Bs";
        }

        public static string NombreMetodo(string metodoPago)
        {
            switch (metodoPago)
            {
                case "option1": return "Punto";
                case "option2": return "Pago Movil";
                case "option3": return "Transferencia";
                case "option4": return "Efectivo";
                case "option5": return "Dolares";
                case "option6": return "Pesos";
                case "option7": return "BioPago";
                default: return metodoPago == "0" ? "Descuento" : "N/D";
            }
        }

        private static double MontoDe(ItemCarrito item, string moneda)
        {
            if (moneda == "USD")
            {
                return item.Price * item.Qty;
            }
            if (moneda == "COP")
            {
                double crudo = item.PricePeso * item.Qty;
                return Math.Round(crudo / 100) * 100;
            }
            return item.PriceBolivar * item.Qty;
        }

        private static string Cabecera(int ancho)
        {
            string izquierda = "#  CANT  PRODUCTO";
            return Ajustar(izquierda, Math.Max(1, ancho - "MONTO".Length)) + "MONTO";
        }

        private static string LineaItem(int numero, ItemCarrito item, double monto, string moneda, int ancho)
        {
            string cantidad = item.Qty == Math.Floor(item.Qty)
                ? ((int)item.Qty).ToString(CultureInfo.InvariantCulture)
                : item.Qty.ToString("0.##", CultureInfo.InvariantCulture);

            string izquierda = numero + "x" + cantidad + " " + (item.Name ?? "");
            string derecha = moneda == "USD" ? "$" + Numero(monto) : Numero(monto);

            if (izquierda.Length + derecha.Length + 1 > ancho)
            {
                izquierda = Ajustar(izquierda, Math.Max(1, ancho - derecha.Length - 1));
            }

            int hueco = ancho - izquierda.Length - derecha.Length;
            return izquierda + new string(' ', Math.Max(1, hueco)) + derecha;
        }

        private static string Derecha(string texto, int ancho)
        {
            texto = texto ?? "";
            return texto.Length >= ancho ? texto : new string(' ', ancho - texto.Length) + texto;
        }

        private static string Ajustar(string texto, int ancho)
        {
            if (texto == null)
            {
                return "";
            }
            if (ancho <= 0)
            {
                return "";
            }
            return texto.Length <= ancho ? texto : texto.Substring(0, ancho);
        }

        private static string Numero(double valor)
        {
            return valor.ToString("N2", Formato);
        }

        /// <summary>ESC/POS básico: solo ASCII. Quita acentos y signos raros.</summary>
        private static byte[] Texto(string texto)
        {
            if (string.IsNullOrEmpty(texto))
            {
                return new byte[0];
            }

            string descompuesto = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);
            foreach (char c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }
                sb.Append(c >= 32 && c <= 126 ? c : '?');
            }
            return Encoding.ASCII.GetBytes(sb.ToString().Normalize(NormalizationForm.FormC));
        }
    }
}
