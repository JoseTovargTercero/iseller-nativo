using System;
using Iseller.Pos.Core.Auth;
using Iseller.Pos.Core.Modelos;
using Xunit;

namespace Iseller.Pos.Tests
{
    public class ClaveLocalTests
    {
        [Fact]
        public void Verifica_la_clave_correcta_y_rechaza_la_incorrecta()
        {
            string sal;
            string hash;
            ClaveLocal.Hashear("123456", out sal, out hash);

            Assert.True(ClaveLocal.Verificar("123456", sal, hash, ClaveLocal.IteracionesPorDefecto));
            Assert.False(ClaveLocal.Verificar("123457", sal, hash, ClaveLocal.IteracionesPorDefecto));
        }

        [Fact]
        public void Dos_hashes_de_la_misma_clave_usan_sales_distintas()
        {
            string sal1, hash1, sal2, hash2;
            ClaveLocal.Hashear("123456", out sal1, out hash1);
            ClaveLocal.Hashear("123456", out sal2, out hash2);

            Assert.NotEqual(sal1, sal2);
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void Datos_corruptos_no_lanzan_excepcion()
        {
            Assert.False(ClaveLocal.Verificar("123456", "no-base64", "tampoco", 100000));
            Assert.False(ClaveLocal.Verificar("123456", "", "", 0));
        }
    }

    public class ReglaSesionTests
    {
        private static SesionLocal Sesion(DateTimeOffset ultimoLogin)
        {
            return new SesionLocal { UltimoLoginValidoEn = ultimoLogin };
        }

        [Fact]
        public void Permite_entrar_offline_dentro_de_dos_dias()
        {
            var ahora = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.FromHours(-4));
            Assert.True(ReglaSesion.PuedeEntrarOffline(Sesion(ahora.AddHours(-47)), ahora));
            Assert.False(ReglaSesion.PuedeEntrarOffline(Sesion(ahora.AddHours(-49)), ahora));
            Assert.False(ReglaSesion.PuedeEntrarOffline(null, ahora));
        }

        [Fact]
        public void Bloquea_la_tasa_a_las_48_horas()
        {
            var ahora = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.FromHours(-4));

            Assert.False(ReglaSesion.TasaVencida(
                new Tasas { ActualizadoEn = ahora.AddHours(-47) }, ahora));
            Assert.True(ReglaSesion.TasaVencida(
                new Tasas { ActualizadoEn = ahora.AddHours(-49) }, ahora));
            Assert.True(ReglaSesion.TasaVencida(new Tasas(), ahora));
        }
    }
}
