using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Iseller.Pos.Core.Actualizacion;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Imagenes;
using Iseller.Pos.Core.Modelos;
using Newtonsoft.Json;
using Xunit;

namespace Iseller.Pos.Tests
{
    public class ReservadosRepositorioTests : IDisposable
    {
        private readonly string _ruta;

        public ReservadosRepositorioTests()
        {
            _ruta = Path.Combine(Path.GetTempPath(), "iseller-reservados-" + Guid.NewGuid().ToString("N") + ".db");
        }

        public void Dispose()
        {
            foreach (var sufijo in new[] { "", "-wal", "-shm" })
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
        public void Guarda_lista_retoma_y_elimina()
        {
            var repo = new ReservadosRepositorio(_ruta);
            var items = new List<ItemCarrito>
            {
                new ItemCarrito { Id = 1, Name = "Arroz", Qty = 2, Price = 1.5, PricePeso = 6000, PriceBolivar = 1500, CantidadPaca = 1 },
            };

            var reservado = new Reservado
            {
                Id = "reserva-1",
                Cliente = "Cliente de prueba",
                CreadaEn = DateTimeOffset.Now,
                ProductosJson = JsonConvert.SerializeObject(items),
            };
            repo.Guardar(reservado);

            var lista = repo.Listar();
            Assert.Single(lista);
            Assert.Equal("Cliente de prueba", lista[0].Cliente);
            Assert.Equal(1, lista[0].CantidadProductos);
            Assert.Equal(3.0, lista[0].TotalUsd, 6);
            Assert.Equal(3000, lista[0].TotalBs, 6);
            Assert.Equal(12000, lista[0].TotalPeso, 6);

            var recuperado = repo.Obtener("reserva-1");
            Assert.NotNull(recuperado);
            Assert.Single(recuperado.Productos());

            repo.Eliminar("reserva-1");
            Assert.Empty(repo.Listar());
            Assert.Equal(0, repo.Contar());
        }
    }

    public class ImagenCacheTests
    {
        [Fact]
        public void La_ruta_local_usa_el_id_y_la_extension()
        {
            var cache = new ImagenCache("http://localhost/iseller-tienda/iseller_store", Path.GetTempPath());
            Assert.EndsWith(".webp", cache.RutaLocal(3305, "assets/img/stock/optimized/3305.webp"));
            Assert.StartsWith("3305-", Path.GetFileName(cache.RutaLocal(3305, "assets/img/stock/optimized/3305.webp")));
            Assert.EndsWith(".png", cache.RutaLocal(77, "foto.png"));
            Assert.EndsWith(".img", cache.RutaLocal(9, "sin-extension"));
        }

        [Fact]
        public void La_misma_foto_da_la_misma_ruta_y_otra_foto_da_otra()
        {
            var cache = new ImagenCache("http://localhost/iseller_store", Path.GetTempPath());

            string a = cache.RutaLocal(3305, "assets/img/stock/optimized/3305.webp");
            string b = cache.RutaLocal(3305, "assets/img/stock/optimized/3305.webp");
            string c = cache.RutaLocal(3305, "assets/img/tiendas/2/otra-foto.webp");

            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }
    }

    public class ActualizadorTests
    {
        [Fact]
        public void Verifica_el_sha256_de_un_archivo()
        {
            string archivo = Path.Combine(Path.GetTempPath(), "iseller-sha-" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllText(archivo, "contenido de prueba");

            string hash;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(archivo))
            {
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }

            try
            {
                Assert.True(Actualizador.VerificarSha256(archivo, hash));
                Assert.True(Actualizador.VerificarSha256(archivo, hash.ToUpperInvariant()));
                Assert.False(Actualizador.VerificarSha256(archivo, "deadbeef"));
                Assert.False(Actualizador.VerificarSha256(archivo, ""));
                Assert.False(Actualizador.VerificarSha256(archivo + ".no-existe", hash));
            }
            finally
            {
                File.Delete(archivo);
            }
        }
    }
}
