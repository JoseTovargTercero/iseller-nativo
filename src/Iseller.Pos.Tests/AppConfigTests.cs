using System;
using System.IO;
using Iseller.Pos.Core.Configuracion;
using Xunit;

namespace Iseller.Pos.Tests
{
    public class AppConfigTests : IDisposable
    {
        private const string Clave = "clave-de-prueba-1234567890";
        private readonly string _ruta;

        public AppConfigTests()
        {
            _ruta = Path.Combine(Path.GetTempPath(), "iseller-config-" + Guid.NewGuid().ToString("N") + ".json");
        }

        public void Dispose()
        {
            foreach (var sufijo in new[] { "", ".bak" })
            {
                try
                {
                    if (File.Exists(_ruta + sufijo))
                    {
                        File.Delete(_ruta + sufijo);
                    }
                }
                catch (IOException)
                {
                }
            }
        }

        [Fact]
        public void La_clave_no_se_guarda_en_claro()
        {
            var config = AppConfig.Cargar(_ruta);
            config.ApiBaseUrl = "https://pos.ejemplo.com/api";
            config.ApiKey = Clave;
            config.Impresora = "POS-58";
            config.Guardar(_ruta);

            string texto = File.ReadAllText(_ruta);

            Assert.DoesNotContain(Clave, texto);
            Assert.Contains("api_key_protegida", texto);
            Assert.Contains("pos.ejemplo.com", texto);
        }

        [Fact]
        public void Recupera_la_clave_cifrada()
        {
            var config = AppConfig.Cargar(_ruta);
            config.ApiKey = Clave;
            config.Guardar(_ruta);

            var leida = AppConfig.Cargar(_ruta);

            Assert.Equal(Clave, leida.ApiKey);
            Assert.Null(leida.AvisoConfiguracion);
        }

        [Fact]
        public void Migra_la_clave_en_claro_de_un_archivo_viejo()
        {
            File.WriteAllText(_ruta,
                "{\"api_base_url\":\"https://pos.ejemplo.com/api\"," +
                "\"api_key\":\"" + Clave + "\"}");

            var config = AppConfig.Cargar(_ruta);

            Assert.Equal(Clave, config.ApiKey);

            // El archivo queda reescrito: cifrado y sin la clave en claro.
            string texto = File.ReadAllText(_ruta);
            Assert.DoesNotContain(Clave, texto);
            Assert.Contains("api_key_protegida", texto);
            Assert.Equal(Clave, AppConfig.Cargar(_ruta).ApiKey);
        }

        [Fact]
        public void Sin_clave_no_escribe_el_campo()
        {
            var config = AppConfig.Cargar(_ruta);
            config.ApiKey = "";
            config.Guardar(_ruta);

            string texto = File.ReadAllText(_ruta);
            Assert.DoesNotContain("api_key_protegida", texto);
            Assert.DoesNotContain("\"api_key\"", texto);
        }

        [Fact]
        public void Una_clave_cifrada_ilegible_no_rompe_la_carga()
        {
            File.WriteAllText(_ruta,
                "{\"api_base_url\":\"https://pos.ejemplo.com/api\"," +
                "\"api_key_protegida\":\"esto-no-es-dpapi\"}");

            var config = AppConfig.Cargar(_ruta);

            Assert.Equal("", config.ApiKey);
            Assert.False(string.IsNullOrEmpty(config.AvisoConfiguracion));
        }
    }
}
