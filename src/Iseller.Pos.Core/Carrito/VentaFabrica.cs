using System;
using System.Collections.Generic;
using Iseller.Pos.Core.Modelos;
using Newtonsoft.Json;

namespace Iseller.Pos.Core.Carrito
{
    /// <summary>Arma la venta a partir del carrito, con precios y tasas congelados.</summary>
    public static class VentaFabrica
    {
        public static Venta Crear(
            Carrito carrito,
            string metodoPago,
            int despacho,
            Cliente cliente,
            SesionLocal sesion,
            Tasas tasas,
            DateTimeOffset ahora,
            IDictionary<string, double> pagosFraccionados = null)
        {
            if (carrito == null || carrito.Vacio)
            {
                throw new InvalidOperationException("El carrito está vacío.");
            }
            if (sesion == null)
            {
                throw new ArgumentNullException(nameof(sesion));
            }

            return new Venta
            {
                IdPedido = Guid.NewGuid().ToString(),
                FechaLocal = ahora,
                MetodoPago = metodoPago ?? "",
                Despacho = despacho,
                TotalUsd = carrito.TotalUsd,
                TotalBs = carrito.TotalBs,
                TotalCop = carrito.TotalPeso,
                ClienteCedula = cliente != null ? (cliente.Cedula ?? "") : "",
                ClienteNombre = cliente != null ? (cliente.Nombre ?? "") : "",
                ClienteTelefono = cliente != null ? (cliente.Telefono ?? "") : "",
                UsuarioId = sesion.UsuarioId,
                UsuarioNombre = sesion.Nombre,
                Estado = EstadoVenta.Pendiente,
                Intentos = 0,
                TasasJson = Venta.SerializarTasas(tasas),
                ProductosJson = JsonConvert.SerializeObject(carrito.Items),
                PagosJson = pagosFraccionados == null ? null : JsonConvert.SerializeObject(pagosFraccionados),
                CreadaEn = ahora,
            };
        }
    }
}
