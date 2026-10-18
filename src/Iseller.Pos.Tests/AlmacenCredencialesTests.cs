using System;
using System.IO;
using Iseller.Pos.Core.Auth;
using Iseller.Pos.Core.Modelos;
using Xunit;

namespace Iseller.Pos.Tests
{
    public class AlmacenCredencialesTests : IDisposable
    {
        private readonly string _ruta;

        public AlmacenCredencialesTests()
        {
            _ruta = Path.Combine(Path.GetTempPath(), "iseller-sesiones-" + Guid.NewGuid().ToString("N") + ".dat");
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_ruta))
                {
                    File.Delete(_ruta);
                }
            }
            catch (IOException)
            {
            }
        }

        private static SesionLocal Sesion(int id, string usuario, string nombre)
        {
            return new SesionLocal
            {
                UsuarioId = id,
                Usuario = usuario,
                Nombre = nombre,
                BssId = 3,
                SucursalId = 9,
                Token = "token-" + id,
            };
        }

        [Fact]
        public void Guarda_varios_cajeros_en_el_mismo_pos()
        {
            var almacen = new AlmacenCredenciales(_ruta);
            almacen.Guardar(Sesion(15, "ricardo", "Ricardo"));
            almacen.Guardar(Sesion(24, "fely", "Fely"));

            Assert.Equal(2, almacen.CargarTodas().Count);
            Assert.Equal(24, almacen.Buscar("FELY").UsuarioId);   // sin distinguir mayúsculas
            Assert.Equal(15, almacen.BuscarPorId(15).UsuarioId);
            Assert.Equal(3, almacen.Buscar("fely").BssId);
            Assert.Equal(9, almacen.Buscar("fely").SucursalId);
            Assert.Null(almacen.Buscar("noexiste"));
        }

        [Fact]
        public void Reemplaza_la_sesion_del_mismo_usuario_y_borra_individualmente()
        {
            var almacen = new AlmacenCredenciales(_ruta);
            almacen.Guardar(Sesion(15, "ricardo", "Ricardo"));
            almacen.Guardar(Sesion(24, "fely", "Fely"));

            almacen.Guardar(Sesion(15, "ricardo", "Ricardo 2"));
            Assert.Equal(2, almacen.CargarTodas().Count);
            Assert.Equal("Ricardo 2", almacen.BuscarPorId(15).Nombre);

            almacen.Borrar(24);
            Assert.Single(almacen.CargarTodas());
            Assert.Null(almacen.BuscarPorId(24));
        }
    }
}
