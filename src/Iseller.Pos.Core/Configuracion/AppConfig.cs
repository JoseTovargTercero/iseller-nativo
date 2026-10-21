using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Impresion;
using Newtonsoft.Json;

namespace Iseller.Pos.Core.Configuracion
{
    /// <summary>
    /// Configuración de la caja: servidor, clave de la API, impresora y cajón.
    ///
    /// La clave de la API no se guarda en claro ni se incluye en el ejecutable: se cifra con
    /// DPAPI del usuario de Windows (la misma protección que usa el POS para las sesiones).
    /// Solo se puede leer desde esta cuenta en este equipo.
    /// </summary>
    public sealed class AppConfig
    {
        public const string UrlPorDefecto = "http://localhost/iseller-tiendas.com/api";

        private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("iSellerPOS.api");

        [JsonProperty("api_base_url")]
        public string ApiBaseUrl { get; set; }

        /// <summary>Clave de la API en memoria. Nunca se escribe en claro en el archivo.</summary>
        [JsonIgnore]
        public string ApiKey { get; set; }

        /// <summary>Clave de la API cifrada (DPAPI del usuario actual, en base64).</summary>
        [JsonProperty("api_key_protegida", NullValueHandling = NullValueHandling.Ignore)]
        public string ApiKeyProtegida { get; set; }

        /// <summary>Solo se usa para migrar archivos viejos con la clave en texto plano.</summary>
        [JsonProperty("api_key", NullValueHandling = NullValueHandling.Ignore)]
        public string ApiKeyEnClaro { get; set; }

        [JsonProperty("impresora")]
        public string Impresora { get; set; }

        /// <summary>58 u 80 mm.</summary>
        [JsonProperty("ancho_mm")]
        public int AnchoMm { get; set; }

        /// <summary>Null = no estaba en el archivo (se asume que sí).</summary>
        [JsonProperty("abrir_cajon", NullValueHandling = NullValueHandling.Ignore)]
        public bool? AbrirCajon { get; set; }

        /// <summary>Motivo para mostrar en pantalla si la configuración no sirve.</summary>
        [JsonIgnore]
        public string AvisoConfiguracion { get; private set; }

        public static string Ruta
        {
            get { return Path.Combine(BasePos.Directorio, "config.json"); }
        }

        public static AppConfig Cargar(string ruta = null)
        {
            ruta = ruta ?? Ruta;

            try
            {
                if (File.Exists(ruta))
                {
                    var config = JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText(ruta));
                    if (config != null)
                    {
                        config.Normalizar();
                        config.ResolverClave(ruta);
                        return config;
                    }
                }
            }
            catch (Exception)
            {
                // Config ilegible: se usan los valores por defecto.
            }

            var nuevo = new AppConfig { ApiBaseUrl = UrlPorDefecto, ApiKey = "" };
            nuevo.Normalizar();
            return nuevo;
        }

        public void Guardar(string ruta = null)
        {
            ruta = ruta ?? Ruta;

            Normalizar();

            // Nunca se escribe la clave en claro: se cifra con el usuario de Windows.
            ApiKeyProtegida = string.IsNullOrWhiteSpace(ApiKey) ? null : Proteger(ApiKey);
            ApiKeyEnClaro = null;

            if (!Directory.Exists(BasePos.Directorio))
            {
                Directory.CreateDirectory(BasePos.Directorio);
            }

            File.WriteAllText(ruta, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        /// <summary>
        /// Deja ApiKey en memoria: migra la clave en claro de archivos viejos (y reescribe el
        /// archivo ya cifrado) o descifra la guardada.
        /// </summary>
        private void ResolverClave(string ruta)
        {
            if (!string.IsNullOrWhiteSpace(ApiKeyEnClaro))
            {
                ApiKey = ApiKeyEnClaro.Trim();
                ApiKeyEnClaro = null;
                try
                {
                    Guardar(ruta);
                }
                catch (Exception)
                {
                    // Si no se pudo reescribir, al menos la clave queda usable en esta sesión.
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(ApiKeyProtegida))
            {
                ApiKey = "";
                return;
            }

            string clave;
            if (Desproteger(ApiKeyProtegida, out clave))
            {
                ApiKey = clave;
                return;
            }

            ApiKey = "";
            AvisoConfiguracion = "La clave de la API guardada en este equipo no se pudo leer " +
                                 "(está cifrada para otro usuario de Windows). Vuelve a colocar config.json.";
        }

        private static string Proteger(string texto)
        {
            byte[] plano = Encoding.UTF8.GetBytes(texto);
            byte[] cifrado = ProtectedData.Protect(plano, Entropia, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(cifrado);
        }

        private static bool Desproteger(string base64, out string texto)
        {
            texto = "";
            try
            {
                byte[] cifrado = Convert.FromBase64String(base64);
                byte[] plano = ProtectedData.Unprotect(cifrado, Entropia, DataProtectionScope.CurrentUser);
                texto = Encoding.UTF8.GetString(plano);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public OpcionesImpresion ComoOpcionesImpresion()
        {
            return new OpcionesImpresion
            {
                Impresora = Impresora ?? "",
                AnchoMm = AnchoMm >= 80 ? 80 : 58,
                AbrirCajon = AbrirCajon ?? true,
                ImprimirTickets = true,
            };
        }

        /// <summary>Base de las fotos de producto en el hosting (iseller_store).</summary>
        [JsonIgnore]
        public string BaseImagenes
        {
            get
            {
                string url = (ApiBaseUrl ?? "").TrimEnd('/');
                if (url.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                {
                    url = url.Substring(0, url.Length - 4);
                }
                return url + "/iseller_store/";
            }
        }

        private void Normalizar()
        {
            if (string.IsNullOrWhiteSpace(ApiBaseUrl))
            {
                ApiBaseUrl = UrlPorDefecto;
            }
            if (ApiKey == null)
            {
                ApiKey = "";
            }
            if (Impresora == null)
            {
                Impresora = "";
            }
            if (AnchoMm != 80)
            {
                AnchoMm = 58;
            }
        }
    }
}
