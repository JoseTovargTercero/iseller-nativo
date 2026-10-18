using System.Collections.Generic;
using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.Core.Carrito
{
    /// <summary>
    /// Mismas validaciones que validarEstructuraVenta() del POS web, para que
    /// ninguna venta llegue al servidor con la estructura equivocada.
    /// </summary>
    public static class ValidadorVenta
    {
        public sealed class Resultado
        {
            public bool Valido { get; set; }
            public string Error { get; set; }
        }

        public static Resultado Validar(Venta venta)
        {
            if (venta == null)
            {
                return Falla("La venta no es un objeto válido.");
            }
            if (string.IsNullOrWhiteSpace(venta.IdPedido))
            {
                return Falla("Falta el ID único del pedido (id_pedido).");
            }
            if (string.IsNullOrWhiteSpace(venta.MetodoPago))
            {
                return Falla("Pedido [" + venta.IdPedido + "]: el método de pago es obligatorio.");
            }
            if (venta.Despacho == 0)
            {
                return Falla("Pedido [" + venta.IdPedido + "]: falta el despacho.");
            }

            List<ItemCarrito> productos = venta.Productos();
            if (productos.Count == 0)
            {
                return Falla("Pedido [" + venta.IdPedido + "]: el listado de productos está vacío.");
            }

            for (int i = 0; i < productos.Count; i++)
            {
                ItemCarrito p = productos[i];
                string ruta = "Pedido [" + venta.IdPedido + "] -> Producto [" + i + "]";

                if (p.Id <= 0)
                {
                    return Falla(ruta + ": falta el id del producto.");
                }
                if (string.IsNullOrWhiteSpace(p.Name))
                {
                    return Falla(ruta + ": falta el nombre del producto.");
                }
                if (p.Qty <= 0)
                {
                    return Falla(ruta + ": la cantidad debe ser mayor a cero.");
                }
                if (p.Price <= 0 || p.PricePeso <= 0 || p.PriceBolivar <= 0)
                {
                    return Falla(ruta + ": faltan precios (price, pricePeso o priceBolivar).");
                }
                if (p.Mayor == "1" && p.CantidadPaca <= 0)
                {
                    return Falla(ruta + ": está marcado como venta al mayor, pero cantidadPaca es inválido.");
                }
            }

            if (venta.Despacho == Despacho.Credito)
            {
                if (string.IsNullOrWhiteSpace(venta.ClienteCedula))
                {
                    return Falla("Pedido [" + venta.IdPedido + "]: la cédula es obligatoria para ventas a crédito.");
                }
                if (string.IsNullOrWhiteSpace(venta.ClienteNombre))
                {
                    return Falla("Pedido [" + venta.IdPedido + "]: el nombre es obligatorio para ventas a crédito.");
                }
            }

            return new Resultado { Valido = true };
        }

        private static Resultado Falla(string error)
        {
            return new Resultado { Valido = false, Error = error };
        }
    }
}
