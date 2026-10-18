using System.Globalization;

namespace Iseller.Pos.Core.Utilidades
{
    /// <summary>
    /// Validación y conversión de números escritos por el cajero.
    /// Solo se admiten dígitos y, si se permite, un único punto decimal.
    /// </summary>
    public static class Numero
    {
        public static bool EsValido(string texto, bool decimales)
        {
            if (string.IsNullOrEmpty(texto))
            {
                return true;
            }

            int puntos = 0;
            foreach (char c in texto)
            {
                if (c >= '0' && c <= '9')
                {
                    continue;
                }
                if (decimales && c == '.')
                {
                    puntos++;
                    if (puntos > 1)
                    {
                        return false;
                    }
                    continue;
                }
                return false;
            }
            return true;
        }

        /// <summary>Convierte el texto a número; acepta coma o punto y devuelve 0 si no es válido.</summary>
        public static double ADouble(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return 0;
            }

            double valor;
            return double.TryParse(texto.Trim().Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out valor) ? valor : 0;
        }
    }
}
