using System;
using System.IO;
using Iseller.Pos.Core.Datos;
using Newtonsoft.Json;

namespace Iseller.Pos.Core.Configuracion
{
    /// <summary>Configuración de la caja: dónde está el servidor y con qué clave se identifica.</summary>
    public sealed class AppConfig
    {
        public const string UrlPorDefecto = "http://localhost/iseller-tiendas.com/api";

        [JsonProperty("api_base_url")]
        public string ApiBaseUrl { get; set; }

        [JsonProperty("api_key")]
        public string ApiKey { get; set; }

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
                        if (string.IsNullOrWhiteSpace(config.ApiBaseUrl))
                        {
                            config.ApiBaseUrl = UrlPorDefecto;
                        }
                        return config;
                    }
                }
            }
            catch (Exception)
            {
                // Config ilegible: se usan los valores por defecto.
            }

            return new AppConfig { ApiBaseUrl = UrlPorDefecto, ApiKey = "" };
        }

        public void Guardar()
        {
            if (!Directory.Exists(BasePos.Directorio))
            {
                Directory.CreateDirectory(BasePos.Directorio);
            }
            File.WriteAllText(Ruta, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
