using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Modelos;
using Newtonsoft.Json;

namespace Iseller.Pos.Core.Auth
{
    /// <summary>
    /// Sesión local guardada con DPAPI (usuario de Windows). Contiene el token Bearer
    /// y el hash de la clave para poder entrar sin conexión.
    /// </summary>
    public sealed class AlmacenCredenciales
    {
        private static readonly byte[] EntropiaAdicional = Encoding.UTF8.GetBytes("iSellerPOS");

        public static string Ruta
        {
            get { return Path.Combine(BasePos.Directorio, "sesion.dat"); }
        }

        public SesionLocal Cargar()
        {
            if (!File.Exists(Ruta))
            {
                return null;
            }

            try
            {
                byte[] cifrado = File.ReadAllBytes(Ruta);
                byte[] plano = ProtectedData.Unprotect(cifrado, EntropiaAdicional, DataProtectionScope.CurrentUser);
                return JsonConvert.DeserializeObject<SesionLocal>(Encoding.UTF8.GetString(plano));
            }
            catch (Exception)
            {
                // Archivo de otro usuario, corrupto o sin DPAPI: se ignora y se vuelve a loguear.
                return null;
            }
        }

        public void Guardar(SesionLocal sesion)
        {
            if (sesion == null)
            {
                throw new ArgumentNullException(nameof(sesion));
            }
            if (!Directory.Exists(BasePos.Directorio))
            {
                Directory.CreateDirectory(BasePos.Directorio);
            }

            byte[] plano = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(sesion));
            byte[] cifrado = ProtectedData.Protect(plano, EntropiaAdicional, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(Ruta, cifrado);
        }

        public void Borrar()
        {
            if (File.Exists(Ruta))
            {
                File.Delete(Ruta);
            }
        }
    }
}
