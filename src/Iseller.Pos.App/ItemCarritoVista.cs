using System.Globalization;
using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.App
{
    /// <summary>Fila del carrito con los subtotales ya formateados.</summary>
    public sealed class ItemCarritoVista
    {
        private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-VE");

        public ItemCarrito Item { get; set; }

        public string Cantidad
        {
            get
            {
                return Item.Qty == System.Math.Floor(Item.Qty)
                    ? ((int)Item.Qty).ToString(Formato)
                    : Item.Qty.ToString("0.##", Formato);
            }
        }

        public string Nombre
        {
            get { return Item.Name + (Item.EsCombo == 1 ? "  [Combo]" : ""); }
        }

        public string Peso
        {
            // Los pesos se muestran sin decimales, como en el carrito del POS web.
            get { return Item.SubtotalPeso.ToString("N0", Formato) + " Cop"; }
        }

        public string Bolivares
        {
            get { return Numero(Item.SubtotalBs) + " Bs"; }
        }

        public string Dolares
        {
            get { return "$" + Numero(Item.SubtotalUsd); }
        }

        private static string Numero(double valor)
        {
            return valor.ToString("N2", Formato);
        }
    }
}
