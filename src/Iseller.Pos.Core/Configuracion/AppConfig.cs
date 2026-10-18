using System;
using System.IO;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Impresion;
using Newtonsoft.Json;

namespace Iseller.Pos.Core.Configuracion
{
    /// <summary>Configuración de la caja: servidor, impresora y cajón.</summary>
    public sealed class AppConfig
    {
        public const string UrlPorDefecto = "http://localhost/iseller-tiendas.com/api";

        [JsonProperty("api_base_url")]
        public string ApiBaseUrl { get; set; }

        [JsonProperty("api_key")]
        public string ApiKey { get; set; }

        [JsonProperty("impresora")]
        public string Impresora { get; set; }

        /// <summary>58 u 80 mm.</summary>
        [JsonProperty("ancho_mm")]
        public int AnchoMm { get; set; }

        /// <summary>Null = no estaba en el archivo (se asume que sí).</summary>
        [JsonProperty("abrir_cajon")]
        public bool? AbrirCajon { get; set; }

        public static string Ruta
        {
            get { return Path.Combine(BasePos.Directorio, "config.json"); }
        }

        public static AppConfig Cargar()
        {
            try
            {
                if (File.Exists(Ruta))
                {
                    var config = JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText(Ruta));
                    if (config != null)
                    {
                        config.Normalizar();
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

        public void Guardar()
        {
            Normalizar();
            if (!Directory.Exists(BasePos.Directorio))
            {
                Directory.CreateDirectory(BasePos.Directorio);
            }
            File.WriteAllText(Ruta, JsonConvert.SerializeObject(this, Formatting.Indented));
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
