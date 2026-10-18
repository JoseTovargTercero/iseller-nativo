using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Modelos;
using Newtonsoft.Json;

namespace Iseller.Pos.Core.Auth
{
    /// <summary>
    /// Sesiones guardadas con DPAPI (usuario de Windows). Guarda una entrada por cajero,
    /// porque en un mismo POS puede iniciar sesión más de una persona por día: cada una
    /// conserva su token, su negocio y su sucursal para poder vender y sincronizar sin conexión.
    /// </summary>
    public sealed class AlmacenCredenciales
    {
        private static readonly byte[] EntropiaAdicional = Encoding.UTF8.GetBytes("iSellerPOS");

        private readonly string _ruta;

        public AlmacenCredenciales(string ruta = null)
        {
            _ruta = ruta ?? Path.Combine(BasePos.Directorio, "sesiones.dat");
        }

        public string Ruta
        {
            get { return _ruta; }
        }

        public List<SesionLocal> CargarTodas()
        {
            var lista = new List<SesionLocal>();

            if (File.Exists(_ruta))
            {
                try
                {
                    byte[] plano = ProtectedData.Unprotect(File.ReadAllBytes(_ruta), EntropiaAdicional, DataProtectionScope.CurrentUser);
                    lista = JsonConvert.DeserializeObject<List<SesionLocal>>(Encoding.UTF8.GetString(plano))
                            ?? new List<SesionLocal>();
                }
                catch (Exception)
                {
                    // Archivo de otro usuario, corrupto o sin DPAPI: se empieza de cero.
                    lista = new List<SesionLocal>();
                }
            }

            if (lista.Count == 0)
            {
                var migrada = MigrarSesionAnterior();
                if (migrada != null)
                {
                    lista.Add(migrada);
                    Guardar(migrada);
                }
            }

            return lista;
        }

        public SesionLocal Buscar(string usuario)
        {
            if (string.IsNullOrWhiteSpace(usuario))
            {
                return null;
            }
            return CargarTodas().FirstOrDefault(s =>
                string.Equals(s.Usuario, usuario.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public SesionLocal BuscarPorId(int usuarioId)
        {
            return CargarTodas().FirstOrDefault(s => s.UsuarioId == usuarioId);
        }

        /// <summary>Agrega o reemplaza la sesión de ese usuario, sin tocar las de los demás.</summary>
        public void Guardar(SesionLocal sesion)
        {
            if (sesion == null)
            {
                throw new ArgumentNullException(nameof(sesion));
            }

            var lista = CargarTodas();
            lista.RemoveAll(s => s.UsuarioId == sesion.UsuarioId ||
                                 string.Equals(s.Usuario, sesion.Usuario, StringComparison.OrdinalIgnoreCase));
            lista.Add(sesion);
            Escribir(lista);
        }

        public void Borrar(int usuarioId)
        {
            var lista = CargarTodas();
            lista.RemoveAll(s => s.UsuarioId == usuarioId);
            Escribir(lista);
        }

        public void BorrarTodas()
        {
            if (File.Exists(_ruta))
            {
                File.Delete(_ruta);
            }
        }

        private void Escribir(List<SesionLocal> lista)
        {
            string carpeta = Path.GetDirectoryName(_ruta);
            if (!string.IsNullOrEmpty(carpeta) && !Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }

            byte[] plano = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(lista));
            byte[] cifrado = ProtectedData.Protect(plano, EntropiaAdicional, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_ruta, cifrado);
        }

        /// <summary>Convierte el formato viejo (una sola sesión) si todavía está en disco.</summary>
        private SesionLocal MigrarSesionAnterior()
        {
            string anterior = Path.Combine(Path.GetDirectoryName(_ruta) ?? ".", "sesion.dat");
            if (!File.Exists(anterior))
            {
                return null;
            }

            try
            {
                byte[] plano = ProtectedData.Unprotect(File.ReadAllBytes(anterior), EntropiaAdicional, DataProtectionScope.CurrentUser);
                var sesion = JsonConvert.DeserializeObject<SesionLocal>(Encoding.UTF8.GetString(plano));
                if (sesion != null)
                {
                    File.Delete(anterior);
                    return sesion;
                }
            }
            catch (Exception)
            {
                // Si no se puede leer, se ignora: se volverá a iniciar sesión.
            }
            return null;
        }
    }
}
