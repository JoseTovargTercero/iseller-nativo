using System;
using System.Collections.Generic;
using System.IO;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Modelos;
using Xunit;

namespace Iseller.Pos.Tests
{
    public class CatalogoRepositorioTests : IDisposable
    {
        private readonly string _ruta;

        public CatalogoRepositorioTests()
        {
            _ruta = Path.Combine(Path.GetTempPath(), "iseller-test-" + Guid.NewGuid().ToString("N") + ".db");
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
                    // El pool de conexiones puede retener el archivo un instante; no afecta al test.
                }
            }
        }

        private static Tasas TasasDePrueba()
        {
            return new Tasas
            {
                PesoDolar = 4000,
                PesoBolivar = 0.0058,
                DolarBolivar = 1000,
                BolivarPeso = 0.0043,
                Bcv = 1000,
                DataMonedasJson = "{\"precio_bolivar_dolar\":\"1\"}",
                PrecioDolarVisible = false,
                PrecioPesoVisible = false,
                ActualizadoEn = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.FromHours(-4)),
            };
        }

        private static List<Producto> ProductosDePrueba()
        {
            return new List<Producto>
            {
                new Producto
                {
                    Id = 1,
                    Nombre = "Jabón de baño",
                    CodigoBarras = "7591234567890",
                    Origen = "v",
                    PrecioCompra = 10,
                    CantidadUnidades = 1,
                    Porcentaje = 30,
                    Mayor = "0",
                    Stock = 24,
                },
                new Producto
                {
                    Id = 2,
                    Nombre = "Combo desayuno",
                    CodigoBarras = "",
                    Origen = "c",
                    PrecioCompra = 3.2859,
                    CantidadUnidades = 1,
                    Porcentaje = 0,
                    Mayor = "",
                    EsCombo = 1,
                    Stock = 24,
                    ComboItems = new List<ItemCombo>
                    {
                        new ItemCombo { Nombre = "Harina Pan", Cantidad = 1 },
                        new ItemCombo { Nombre = "Galleta Babo", Cantidad = 2 },
                    },
                },
            };
        }

        private void Guardar(CatalogoRepositorio repo, DateTimeOffset servidorEn)
        {
            repo.GuardarCatalogo(
                ProductosDePrueba(),
                new List<Cliente> { new Cliente { Cedula = "V123", Nombre = "Ana", Telefono = "0414" } },
                TasasDePrueba(),
                new ConfiguracionNegocio { Tickets = 1, CortesCaja = 1 },
                9,
                "hash-de-prueba",
                servidorEn);
        }

        [Fact]
        public void Guarda_y_recupera_el_catalogo()
        {
            var repo = new CatalogoRepositorio(_ruta);
            var servidorEn = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.FromHours(-4));
            Guardar(repo, servidorEn);

            Assert.Equal(2, repo.ContarProductos());
            Assert.Equal("hash-de-prueba", repo.Version());
            Assert.Equal(servidorEn, repo.ServidorEn().Value);
            Assert.Equal(9, repo.SucursalId());
            Assert.Equal(1, repo.ObtenerConfiguracion().Tickets);

            var tasas = repo.ObtenerTasas();
            Assert.NotNull(tasas);
            Assert.Equal(4000, tasas.PesoDolar, 6);
            Assert.False(tasas.PrecioDolarVisible);
            Assert.Equal("{\"precio_bolivar_dolar\":\"1\"}", tasas.DataMonedasJson);
            Assert.Equal(servidorEn, tasas.ActualizadoEn.Value);

            var cliente = repo.ObtenerCliente("V123");
            Assert.NotNull(cliente);
            Assert.Equal("Ana", cliente.Nombre);
        }

        [Fact]
        public void Busca_sin_acentos_y_por_codigo()
        {
            var repo = new CatalogoRepositorio(_ruta);
            Guardar(repo, DateTimeOffset.Now);

            var porNombre = repo.Buscar("jabon");
            Assert.Single(porNombre);
            Assert.Equal(1, porNombre[0].Id);

            var porCodigo = repo.Buscar("759123");
            Assert.Single(porCodigo);
            Assert.Equal(1, porCodigo[0].Id);

            var exacto = repo.BuscarPorCodigo("7591234567890");
            Assert.NotNull(exacto);
            Assert.Equal(1, exacto.Id);

            Assert.Empty(repo.Buscar("noexiste"));
        }

        [Fact]
        public void Un_digito_en_el_nombre_no_arrastra_codigos()
        {
            var repo = new CatalogoRepositorio(_ruta);
            Guardar(repo, DateTimeOffset.Now);

            // Antes, el "2" hacía LIKE sobre códigos de barras y devolvía media tienda.
            Assert.Empty(repo.Buscar("jabon 2"));
            Assert.Single(repo.Buscar("jabon"));
        }

        [Fact]
        public void Conserva_los_items_del_combo()
        {
            var repo = new CatalogoRepositorio(_ruta);
            Guardar(repo, DateTimeOffset.Now);

            var combos = repo.Buscar("combo desayuno");
            Assert.Single(combos);
            Assert.Equal(1, combos[0].EsCombo);
            Assert.Equal(2, combos[0].ComboItems.Count);
            Assert.Contains(combos[0].ComboItems, i => i.Nombre == "Harina Pan" && i.Cantidad == 1);
            Assert.Contains(combos[0].ComboItems, i => i.Nombre == "Galleta Babo" && i.Cantidad == 2);
        }

        [Fact]
        public void Oculta_los_productos_al_mayor_si_no_tiene_permiso()
        {
            var repo = new CatalogoRepositorio(_ruta);
            var productos = ProductosDePrueba();
            productos.Add(new Producto
            {
                Id = 3,
                Nombre = "Harina al mayor",
                CodigoBarras = "7595550001111",
                Origen = "v",
                PrecioCompra = 10,
                CantidadUnidades = 12,
                Porcentaje = 10,
                Mayor = "1",
                Stock = 5,
            });
            repo.GuardarCatalogo(productos, new List<Cliente>(), TasasDePrueba(),
                new ConfiguracionNegocio(), 9, "v2", DateTimeOffset.Now);

            // El servidor ya los filtra al descargar; esto cubre el catálogo bajado por otro usuario.
            Assert.Equal(3, repo.Productos().Count);
            Assert.Equal(2, repo.Productos(false).Count);
            Assert.Single(repo.Buscar("harina al mayor"));
            Assert.Empty(repo.Buscar("harina al mayor", 50, false));
            Assert.NotNull(repo.BuscarPorCodigo("7595550001111"));
            Assert.Null(repo.BuscarPorCodigo("7595550001111", false));
        }
    }
}
