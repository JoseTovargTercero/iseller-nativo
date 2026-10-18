using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Iseller.Pos.Core.Actualizacion
{
    public sealed class ActualizacionDisponible
    {
        public bool Ok { get; set; }
        public bool Hay { get; set; }
        public string Version { get; set; }
        public string Url { get; set; }
        public string Sha256 { get; set; }
        public string Notas { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Autoactualización desde el servidor: consulta pos_version.php, descarga el ZIP,
    /// valida el SHA-256 y lo aplica con un script al cerrar la app.
    /// </summary>
    public static class Actualizador
    {
        public static async Task<ActualizacionDisponible> BuscarAsync(string apiBase, string apiKey, string versionActual)
        {
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
                {
                    var peticion = new HttpRequestMessage(
                        HttpMethod.Get, (apiBase ?? "").TrimEnd('/') + "/pos_version.php");
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        peticion.Headers.Add("X-Api-Key", apiKey);
                    }

                    using (var respuesta = await http.SendAsync(peticion))
                    {
                        string texto = await respuesta.Content.ReadAsStringAsync();
                        var dto = JsonConvert.DeserializeObject<VersionDto>(texto);
                        if (dto == null || !dto.Ok)
                        {
                            return new ActualizacionDisponible
                            {
                                Error = dto != null && !string.IsNullOrEmpty(dto.Error)
                                    ? dto.Error
                                    : "No se pudo consultar la versión",
                            };
                        }

                        Version remota;
                        Version actual;
                        bool hay = Version.TryParse(dto.Version, out remota)
                                   && Version.TryParse(versionActual, out actual)
                                   && remota > actual;

                        return new ActualizacionDisponible
                        {
                            Ok = true,
                            Hay = hay,
                            Version = dto.Version,
                            Url = dto.Url,
                            Sha256 = dto.Sha256,
                            Notas = dto.Notas,
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ActualizacionDisponible { Error = ex.Message };
            }
        }

        /// <summary>Descarga el ZIP y devuelve su ruta local.</summary>
        public static async Task<string> DescargarAsync(string url, string destino)
        {
            string carpeta = Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            using (var respuesta = await http.GetAsync(url))
            {
                respuesta.EnsureSuccessStatusCode();
                byte[] bytes = await respuesta.Content.ReadAsByteArrayAsync();
                File.WriteAllBytes(destino, bytes);
            }
            return destino;
        }

        public static bool VerificarSha256(string archivo, string esperado)
        {
            if (string.IsNullOrWhiteSpace(esperado) || !File.Exists(archivo))
            {
                return false;
            }

            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(archivo))
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                return string.Equals(hash, esperado.Trim().ToLowerInvariant(), StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Lanza un script que espera a que la app cierre, descomprime el ZIP sobre
        /// la carpeta actual y vuelve a abrir el ejecutable.
        /// </summary>
        public static void Aplicar(string zip, string ejecutable)
        {
            string destino = Path.GetDirectoryName(ejecutable);
            string nombreExe = Path.GetFileName(ejecutable);
            string script = Path.Combine(Path.GetTempPath(), "iseller-update-" + Guid.NewGuid().ToString("N") + ".cmd");

            string contenido =
                "@echo off\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                "taskkill /f /im \"" + nombreExe + "\" >nul 2>&1\r\n" +
                "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Expand-Archive -LiteralPath '" +
                zip.Replace("'", "''") + "' -DestinationPath '" + destino.Replace("'", "''") + "' -Force\"\r\n" +
                "start \"\" \"" + ejecutable + "\"\r\n" +
                "del \"%~f0\"\r\n";

            File.WriteAllText(script, contenido);
            Process.Start(new ProcessStartInfo("cmd.exe", "/c \"\"" + script + "\"\"")
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            });
        }

        private sealed class VersionDto
        {
            [JsonProperty("ok")] public bool Ok { get; set; }
            [JsonProperty("version")] public string Version { get; set; }
            [JsonProperty("url")] public string Url { get; set; }
            [JsonProperty("sha256")] public string Sha256 { get; set; }
            [JsonProperty("notas")] public string Notas { get; set; }
            [JsonProperty("error")] public string Error { get; set; }
        }
    }
}
