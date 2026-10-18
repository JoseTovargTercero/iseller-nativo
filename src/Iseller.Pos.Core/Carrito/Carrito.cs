using System;
using System.Collections.Generic;
using Iseller.Pos.Core.Modelos;
using Iseller.Pos.Core.Precios;

namespace Iseller.Pos.Core.Carrito
{
    /// <summary>Carrito de la venta en curso.</summary>
    public sealed class Carrito
    {
        private readonly Dictionary<int, ItemCarrito> _items = new Dictionary<int, ItemCarrito>();

        public ICollection<ItemCarrito> Items
        {
            get { return _items.Values; }
        }

        public int CantidadItems
        {
            get { return _items.Count; }
        }

        public bool Vacio
        {
            get { return _items.Count == 0; }
        }

        public double TotalUsd
        {
            get
            {
                double total = 0;
                foreach (var item in _items.Values)
                {
                    total += item.SubtotalUsd;
                }
                return total;
            }
        }

        public double TotalBs
        {
            get
            {
                double total = 0;
                foreach (var item in _items.Values)
                {
                    total += item.SubtotalBs;
                }
                return total;
            }
        }

        public double TotalPeso
        {
            get
            {
                double total = 0;
                foreach (var item in _items.Values)
                {
                    total += item.SubtotalPeso;
                }
                return Math.Round(total);
            }
        }

        public ItemCarrito Obtener(int productoId)
        {
            ItemCarrito item;
            return _items.TryGetValue(productoId, out item) ? item : null;
        }

        /// <summary>Agrega (o suma) un producto con sus precios calculados ahora.</summary>
        public ItemCarrito Agregar(Producto producto, CalculadoraPrecios calculadora, double cantidad)
        {
            if (producto == null)
            {
                throw new ArgumentNullException(nameof(producto));
            }
            if (cantidad <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe ser mayor a cero.");
            }

            var precios = calculadora.Calcular(new ProductoPrecio
            {
                CantidadUnidades = producto.CantidadUnidades,
                Origen = producto.Origen,
                PrecioCompra = producto.PrecioCompra,
                Porcentaje = producto.Porcentaje,
                Mayor = producto.Mayor,
            });

            double valorUnidad = producto.CantidadUnidades > 0
                ? producto.PrecioCompra / producto.CantidadUnidades
                : producto.PrecioCompra;

            ItemCarrito existente;
            if (_items.TryGetValue(producto.Id, out existente))
            {
                existente.Qty += cantidad;
                return existente;
            }

            var item = new ItemCarrito
            {
                Id = producto.Id,
                Name = producto.Nombre,
                PriceC = valorUnidad,
                PriceCBs = valorUnidad * calculadora.Parametros.DolarBolivar,
                PriceCCop = valorUnidad * calculadora.Parametros.PesoDolar,
                Price = precios.PrecioVentaDolar,
                PricePeso = precios.PrecioVentaPeso,
                PriceBolivar = precios.PrecioVentaBs,
                Qty = cantidad,
                Mayor = producto.Mayor ?? "0",
                CantidadPaca = producto.CantidadUnidades,
                EsCombo = producto.EsCombo,
                ComboItems = new List<ItemCombo>(producto.ComboItems ?? new List<ItemCombo>()),
            };

            _items[producto.Id] = item;
            return item;
        }

        /// <summary>Suma o resta unidades. Devuelve true si el item sigue en el carrito.</summary>
        public bool Ajustar(int productoId, double delta)
        {
            ItemCarrito item;
            if (!_items.TryGetValue(productoId, out item))
            {
                return false;
            }

            item.Qty += delta;
            if (item.Qty <= 0)
            {
                _items.Remove(productoId);
                return false;
            }
            return true;
        }

        public void Quitar(int productoId)
        {
            _items.Remove(productoId);
        }

        public void Vaciar()
        {
            _items.Clear();
        }

        public void Cargar(IEnumerable<ItemCarrito> items)
        {
            _items.Clear();
            if (items == null)
            {
                return;
            }
            foreach (var item in items)
            {
                if (item != null && item.Qty > 0)
                {
                    _items[item.Id] = item;
                }
            }
        }
    }
}
