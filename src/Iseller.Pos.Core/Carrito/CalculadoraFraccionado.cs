using System;
using System.Collections.Generic;

namespace Iseller.Pos.Core.Carrito
{
    /// <summary>
    /// Pago fraccionado (tipoPago 8): port exacto de calcularFraccionado() y
    /// pagosFraccionadosNetos() del POS web. Cada moneda del carrito es un precio
    /// completo, así que un pago cubre la fracción que representa dentro del total
    /// de su propia moneda (no se convierte entre monedas).
    /// </summary>
    public static class CalculadoraFraccionado
    {
        public const string Punto = "punto";
        public const string BioPago = "bioPago";
        public const string PagoMovil = "pagoMovil";
        public const string Transferencia = "transferencia";
        public const string Efectivo = "efectivo";
        public const string Dolares = "dolares";
        public const string Pesos = "pesos";

        /// <summary>Los siete medios del desglose, en el orden del POS web.</summary>
        public static readonly string[] Claves =
        {
            Punto, BioPago, PagoMovil, Transferencia, Efectivo, Dolares, Pesos,
        };

        // Number.EPSILON de JavaScript (Math.round del web le suma esto).
        private const double EpsilonJs = 2.220446049250313e-16;

        public sealed class Resultado
        {
            public Dictionary<string, double> Pagos { get; set; }
            public double Cobertura { get; set; }
            public double Falta { get; set; }
            public bool Saldado { get; set; }
            public double RestanteUsd { get; set; }
            public double RestanteCop { get; set; }
            public double RestanteBs { get; set; }
            public double ExcesoUsd { get; set; }
            public double ExcesoCop { get; set; }
            public double ExcesoBs { get; set; }

            public bool HayExceso
            {
                get { return ExcesoUsd > 0.01 || ExcesoCop > 1 || ExcesoBs > 0.05; }
            }
        }

        /// <summary>Moneda del total a la que cubre cada medio.</summary>
        public static string Moneda(string clave)
        {
            if (clave == Dolares)
            {
                return "usd";
            }
            if (clave == Pesos)
            {
                return "cop";
            }
            return "bs";
        }

        public static Resultado Calcular(IDictionary<string, double> pagos,
                                         double totalUsd, double totalCop, double totalBs)
        {
            var limpios = new Dictionary<string, double>();
            foreach (string clave in Claves)
            {
                double valor;
                limpios[clave] = pagos != null && pagos.TryGetValue(clave, out valor) ? valor : 0;
            }

            var totales = new Dictionary<string, double>
            {
                { "usd", totalUsd },
                { "cop", totalCop },
                { "bs", totalBs },
            };

            var pagosPorMoneda = new Dictionary<string, double>
            {
                { "usd", 0 },
                { "cop", 0 },
                { "bs", 0 },
            };
            foreach (string clave in Claves)
            {
                pagosPorMoneda[Moneda(clave)] += limpios[clave];
            }

            double cobertura = 0;
            foreach (string moneda in new[] { "usd", "cop", "bs" })
            {
                if (totales[moneda] > 0)
                {
                    cobertura += pagosPorMoneda[moneda] / totales[moneda];
                }
            }

            double falta = Math.Max(0, 1 - cobertura);
            var resultado = new Resultado
            {
                Pagos = limpios,
                Cobertura = cobertura,
                Falta = falta,
                Saldado = falta * totales["usd"] <= 0.02,
            };

            if (!resultado.Saldado)
            {
                resultado.RestanteUsd = Redondear2(falta * totales["usd"]);
                resultado.RestanteCop = Math.Max(0, RedondearCentena(falta * totales["cop"]));
                resultado.RestanteBs = Redondear2(falta * totales["bs"]);
            }

            if (cobertura > 1)
            {
                string elegida = null;
                double mejorFraccion = 0;
                double monto = 0;

                foreach (string moneda in new[] { "cop", "bs", "usd" })
                {
                    if (totales[moneda] <= 0)
                    {
                        continue;
                    }

                    double otros = Math.Min(1, cobertura - (pagosPorMoneda[moneda] / totales[moneda]));
                    double requerido = (1 - otros) * totales[moneda];
                    double sobrante = pagosPorMoneda[moneda] - requerido;
                    if (sobrante <= 0)
                    {
                        continue;
                    }

                    double fraccion = sobrante / totales[moneda];
                    if (fraccion > mejorFraccion)
                    {
                        mejorFraccion = fraccion;
                        elegida = moneda;
                        monto = sobrante;
                    }
                }

                if (elegida == "usd")
                {
                    resultado.ExcesoUsd = monto;
                }
                else if (elegida == "cop")
                {
                    resultado.ExcesoCop = monto;
                }
                else if (elegida == "bs")
                {
                    resultado.ExcesoBs = monto;
                }
            }

            resultado.ExcesoUsd = Redondear2(resultado.ExcesoUsd);
            resultado.ExcesoCop = Math.Round(resultado.ExcesoCop, MidpointRounding.AwayFromZero);
            resultado.ExcesoBs = Redondear2(resultado.ExcesoBs);

            return resultado;
        }

        /// <summary>
        /// Lo que se guarda como desglose: el vuelto se descuenta del efectivo de la misma
        /// moneda (para que el cierre de caja cuadre con lo que queda en caja).
        /// </summary>
        public static Dictionary<string, double> Netos(Resultado resultado)
        {
            var neto = new Dictionary<string, double>(resultado.Pagos);

            var orden = new Dictionary<string, string[]>
            {
                { "bs", new[] { Efectivo } },
                { "usd", new[] { Dolares } },
                { "cop", new[] { Pesos } },
            };

            foreach (string moneda in new[] { "usd", "cop", "bs" })
            {
                double exceso = moneda == "usd" ? resultado.ExcesoUsd
                    : moneda == "cop" ? resultado.ExcesoCop
                    : resultado.ExcesoBs;
                double minimo = moneda == "cop" ? 1 : 0.01;
                if (exceso < minimo)
                {
                    continue;
                }

                foreach (string clave in orden[moneda])
                {
                    if (exceso < minimo)
                    {
                        break;
                    }

                    double actual = neto[clave];
                    double quitar = Math.Min(actual, exceso);
                    neto[clave] = Redondear2(actual - quitar);
                    exceso -= quitar;
                }
            }

            return neto;
        }

        /// <summary>Igual que redondearCentena() del POS web.</summary>
        public static double RedondearCentena(double numero)
        {
            double resto = numero % 100;
            return resto >= 50
                ? Math.Ceiling(numero / 100) * 100
                : Math.Floor(numero / 100) * 100;
        }

        /// <summary>Igual que _r2() del POS web (Math.round a dos decimales).</summary>
        public static double Redondear2(double numero)
        {
            return Math.Round((numero + EpsilonJs) * 100, MidpointRounding.AwayFromZero) / 100;
        }
    }
}
