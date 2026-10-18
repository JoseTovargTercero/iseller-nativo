using System;
using System.Collections.Generic;
using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.Core.Carrito
{
    /// <summary>Despachos posibles de una venta (mismo valor que usa el POS web).</summary>
    public static class Despacho
    {
        public const int Venta = 1;
        public const int Credito = 2;
        public const int Descuento = 3;
    }
}
