using System;
using System.Collections.Generic;
using System.IO;
using Iseller.Pos.Core.Caja;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Modelos;
using Xunit;

namespace Iseller.Pos.Tests
{
    public class CajaRepositorioTests : IDisposable
    {
        private readonly string _ruta;

        public CajaRepositorioTests()
        {
            _ruta = Path.Combine(Path.GetTempPath(), "iseller-caja-" + Guid.NewGuid().ToString("N") + ".db");
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

        private static RegistroCaja Movimiento(string tipo, double fondo = 0)
        {
            return new RegistroCaja
            {
                Id = Guid.NewGuid().ToString(),
                Tipo = tipo,
                CreadaEn = DateTimeOffset.Now,
                UsuarioId = 15,
                Observaciones = "prueba",
                Montos = new Dictionary<string, double> { { "efectivo_bs_fondo", fondo } },
            };
        }

        [Fact]
        public void Abre_cierra_y_reabre()
        {
            var repo = new CajaRepositorio(_ruta);
            var hoy = DateTime.Today;

            Assert.Equal(EstadoCaja.SinApertura, repo.Estado(15, hoy));

            repo.Guardar(Movimiento("apertura", 100));
            Assert.Equal(EstadoCaja.Abierta, repo.Estado(15, hoy));

            var cierre = Movimiento("cierre");
            repo.Guardar(cierre);
            Assert.Equal(EstadoCaja.Cerrada, repo.Estado(15, hoy));

            Assert.Equal(2, repo.ContarPendientes());

            var apertura = repo.UltimoDelDia(15, hoy);
            Assert.True(apertura.EsCierre);

            // Reabrir = borrar el cierre local.
            repo.Eliminar(cierre.Id);
            Assert.Equal(EstadoCaja.Abierta, repo.Estado(15, hoy));
        }

        [Fact]
        public void Guarda_montos_y_marca_sincronizado()
        {
            var repo = new CajaRepositorio(_ruta);
            var apertura = Movimiento("apertura", 250);
            repo.Guardar(apertura);

            var pendientes = repo.Pendientes();
            Assert.Single(pendientes);
            Assert.Equal(250, pendientes[0].Monto("efectivo_bs_fondo"), 6);
            Assert.Null(pendientes[0].SincronizadaEn);

            repo.MarcarSincronizada(apertura.Id, DateTimeOffset.Now);
            Assert.Empty(repo.Pendientes());
            Assert.Equal(0, repo.ContarPendientes());
        }

        [Fact]
        public void No_mezcla_el_estado_de_otro_usuario()
        {
            var repo = new CajaRepositorio(_ruta);
            repo.Guardar(Movimiento("apertura"));

            Assert.Equal(EstadoCaja.Abierta, repo.Estado(15, DateTime.Today));
            Assert.Equal(EstadoCaja.SinApertura, repo.Estado(99, DateTime.Today));
        }
    }

    public class CajaReglasTests
    {
        [Fact]
        public void Solo_exige_caja_a_cajeros_con_cortes_activos()
        {
            var activo = new ConfiguracionNegocio { CortesCaja = 1 };
            var inactivo = new ConfiguracionNegocio { CortesCaja = 0 };

            Assert.True(CajaReglas.Requiere(activo, 2));
            Assert.False(CajaReglas.Requiere(activo, 1));
            Assert.False(CajaReglas.Requiere(inactivo, 2));
            Assert.False(CajaReglas.Requiere(null, 2));
        }

        [Fact]
        public void Bloquea_la_venta_si_no_esta_abierta()
        {
            Assert.True(CajaReglas.BloqueaVenta(EstadoCaja.SinApertura));
            Assert.True(CajaReglas.BloqueaVenta(EstadoCaja.Cerrada));
            Assert.False(CajaReglas.BloqueaVenta(EstadoCaja.Abierta));
        }
    }
}
