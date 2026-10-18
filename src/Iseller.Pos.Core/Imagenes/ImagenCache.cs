using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Iseller.Pos.Core.Imagenes
{
    /// <summary>
    /// Caché local de fotos de producto. La primera vez descarga del servidor;
    /// después funciona sin conexión.
    /// </summary>
    public sealed class ImagenCache : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _directorio;
        private readonly SemaphoreSlim _limite = new SemaphoreSlim(4);

        public ImagenCache(string baseUrl, string directorio = null)
        {
            _baseUrl = (baseUrl ?? "").TrimEnd('/') + "/";
            _directorio = directorio ?? Path.Combine(Datos.BasePos.Directorio, "img");
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public string Directorio
        {
            get { return _directorio; }
        }

        /// <summary>Devuelve la ruta local de la imagen, o null si no se pudo obtener.</summary>
        public async Task<string> ObtenerAsync(int id, string foto)
        {
            if (string.IsNullOrWhiteSpace(foto))
            {
                return null;
            }

            string ruta = RutaLocal(id, foto);
            if (File.Exists(ruta))
            {
                return ruta;
            }

            await _limite.WaitAsync();
            try
            {
                if (File.Exists(ruta))
                {
                    return ruta;
                }

                string url = _baseUrl + foto.TrimStart('/');
                using (var respuesta = await _http.GetAsync(url))
                {
                    if (!respuesta.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    byte[] bytes = await respuesta.Content.ReadAsByteArrayAsync();
                    if (bytes.Length == 0)
                    {
                        return null;
                    }
                    Directory.CreateDirectory(_directorio);
                    File.WriteAllBytes(ruta, bytes);
                    LimpiarVariantes(id, ruta);
                    return ruta;
                }
            }
            catch (Exception)
            {
                // Sin conexión o imagen caída: la próxima vez se reintenta.
                return null;
            }
            finally
            {
                _limite.Release();
            }
        }

        public string RutaLocal(int id, string foto)
        {
            string extension = Path.GetExtension(foto ?? "");
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".img";
            }
            // La clave incluye la ruta de la foto: si el negocio la cambia, se descarga la nueva.
            return Path.Combine(_directorio, id + "-" + Huella(foto) + extension);
        }

        private static string Huella(string foto)
        {
            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes((foto ?? "").Trim()));
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 8).ToLowerInvariant();
            }
        }

        /// <summary>Borra variantes viejas de la misma foto (cambios de ruta o del formato anterior).</summary>
        private void LimpiarVariantes(int id, string rutaActual)
        {
            try
            {
                foreach (string patron in new[] { id + "-*", id + ".*" })
                {
                    foreach (string archivo in Directory.GetFiles(_directorio, patron))
                    {
                        if (!string.Equals(archivo, rutaActual, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(archivo);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Limpieza best-effort.
            }
        }

        public void Dispose()
        {
            _http.Dispose();
            _limite.Dispose();
        }
    }
}
