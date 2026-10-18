using System;
using System.Security.Cryptography;

namespace Iseller.Pos.Core.Auth
{
    /// <summary>
    /// Hash de la clave del cajero para el desbloqueo sin conexión.
    /// PBKDF2-SHA256 (el .NET Framework 4.8 no trae Argon2 sin dependencias extra).
    /// </summary>
    public static class ClaveLocal
    {
        public const int IteracionesPorDefecto = 100000;
        private const int LargoHash = 32;
        private const int LargoSal = 16;

        public static void Hashear(string clave, out string salBase64, out string hashBase64, int iteraciones = IteracionesPorDefecto)
        {
            var sal = new byte[LargoSal];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(sal);
            }

            salBase64 = Convert.ToBase64String(sal);
            hashBase64 = Convert.ToBase64String(Derivar(clave, sal, iteraciones));
        }

        public static bool Verificar(string clave, string salBase64, string hashBase64, int iteraciones)
        {
            if (string.IsNullOrEmpty(salBase64) || string.IsNullOrEmpty(hashBase64) || iteraciones <= 0)
            {
                return false;
            }

            byte[] sal;
            byte[] esperado;
            try
            {
                sal = Convert.FromBase64String(salBase64);
                esperado = Convert.FromBase64String(hashBase64);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] calculado = Derivar(clave ?? "", sal, iteraciones);
            return IgualesTiempoConstante(calculado, esperado);
        }

        private static byte[] Derivar(string clave, byte[] sal, int iteraciones)
        {
            using (var derivador = new Rfc2898DeriveBytes(clave ?? "", sal, iteraciones, HashAlgorithmName.SHA256))
            {
                return derivador.GetBytes(LargoHash);
            }
        }

        private static bool IgualesTiempoConstante(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            int diferencia = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diferencia |= a[i] ^ b[i];
            }
            return diferencia == 0;
        }
    }
}
