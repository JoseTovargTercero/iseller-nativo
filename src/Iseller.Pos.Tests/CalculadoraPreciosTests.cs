using Iseller.Pos.Core.Precios;
using Xunit;

namespace Iseller.Pos.Tests
{
    /// <summary>
    /// Valores generados ejecutando el PHP real (configurar/_calculadrora_precios.php)
    /// con tasas pesoDolar=4000, peso_bolivar=25, dolarBolivar=40, bolivar_peso=0.22, bcv=40.
    /// </summary>
    public class CalculadoraPreciosTests
    {
        private static ParametrosPrecios Tasas(bool dolarVisible = false, bool pesoVisible = false)
        {
            return new ParametrosPrecios
            {
                PesoDolar = 4000,
                PesoBolivar = 25,
                DolarBolivar = 40,
                BolivarPeso = 0.22,
                Bcv = 40,
                PrecioDolarVisible = dolarVisible,
                PrecioPesoVisible = pesoVisible
            };
        }

        private static ProductoPrecio Producto(
            double cantidadUnidades, string origen, double precioCompra, double porcentaje, string mayor)
        {
            return new ProductoPrecio
            {
                CantidadUnidades = cantidadUnidades,
                Origen = origen,
                PrecioCompra = precioCompra,
                Porcentaje = porcentaje,
                Mayor = mayor
            };
        }

        private static void Iguales(PreciosCalculados esperado, PreciosCalculados real)
        {
            Assert.Equal(esperado.PrecioVentaDolar, real.PrecioVentaDolar, 6);
            Assert.Equal(esperado.PrecioVentaBs, real.PrecioVentaBs, 6);
            Assert.Equal(esperado.PrecioVentaPeso, real.PrecioVentaPeso, 6);
        }

        [Fact]
        public void Venezolano_detal_sin_flags()
        {
            var calc = new CalculadoraPrecios(Tasas());
            var r = calc.Calcular(Producto(1, "v", 10, 30, "0"));

            Iguales(new PreciosCalculados
            {
                PrecioVentaDolar = 13,
                PrecioVentaBs = 520,
                PrecioVentaPeso = 114400
            }, r);
        }

        [Fact]
        public void Venezolano_detal_con_flags()
        {
            var calc = new CalculadoraPrecios(Tasas(true, true));
            var r = calc.Calcular(Producto(1, "v", 10, 30, "0"));

            Iguales(new PreciosCalculados
            {
                PrecioVentaDolar = 13,
                PrecioVentaBs = 520,
                PrecioVentaPeso = 52000
            }, r);
        }

        [Fact]
        public void Colombiano_detal()
        {
            var calc = new CalculadoraPrecios(Tasas(true, true));
            var r = calc.Calcular(Producto(24, "c", 12, 20, "0"));

            Iguales(new PreciosCalculados
            {
                PrecioVentaDolar = 0.6,
                PrecioVentaBs = 0.096,
                PrecioVentaPeso = 2400
            }, r);
        }

        [Fact]
        public void Colombiano_al_mayor()
        {
            var calc = new CalculadoraPrecios(Tasas(true, true));
            var r = calc.Calcular(Producto(24, "c", 12, 20, "1"));

            Iguales(new PreciosCalculados
            {
                PrecioVentaDolar = 14.4,
                PrecioVentaBs = 2.304,
                PrecioVentaPeso = 57600
            }, r);
        }

        [Fact]
        public void Producto_barato_aplica_minimo_de_100_pesos()
        {
            var calc = new CalculadoraPrecios(Tasas(true, true));
            var r = calc.Calcular(Producto(1, "v", 0.01, 0, "0"));

            Iguales(new PreciosCalculados
            {
                PrecioVentaDolar = 0.01,
                PrecioVentaBs = 0.4,
                PrecioVentaPeso = 100
            }, r);
        }

        [Fact]
        public void Pesos_trunca_a_entero_antes_del_modulo_como_php()
        {
            // 0.12672 * 4000 = 506.88 -> PHP hace (int)506.88 % 100 = 6 y redondea a 500.
            var calc = new CalculadoraPrecios(Tasas(true, true));
            var r = calc.Calcular(Producto(1, "v", 0.12672, 0, "0"));

            Assert.Equal(500, r.PrecioVentaPeso, 6);
        }

        [Fact]
        public void Precio_dolar_mayor_a_999_se_calcula_correctamente()
        {
            // Regresion: number_format("6,500.00") casteado a float devolvia 6.
            var calc = new CalculadoraPrecios(Tasas(true, true));
            var r = calc.Calcular(Producto(1, "v", 5000, 30, "0"));

            Iguales(new PreciosCalculados
            {
                PrecioVentaDolar = 6500,
                PrecioVentaBs = 260000,
                PrecioVentaPeso = 26000000
            }, r);
        }

        [Fact]
        public void Precio_dolar_derivado_de_bolivares_mayor_a_999_se_calcula_correctamente()
        {
            var calc = new CalculadoraPrecios(Tasas());
            var r = calc.Calcular(Producto(1, "v", 5000, 30, "0"));

            Assert.Equal(6500, r.PrecioVentaDolar, 6);
        }

        [Fact]
        public void Sin_flags()
        {
            var calc = new CalculadoraPrecios(Tasas());
            var r = calc.Calcular(Producto(6, "v", 60, 15, "0"));

            Iguales(new PreciosCalculados
            {
                PrecioVentaDolar = 11.5,
                PrecioVentaBs = 460,
                PrecioVentaPeso = 101200
            }, r);
        }

        [Fact]
        public void Caso_real_harina_tradicional_sucursal_8()
        {
            // Datos reales del negocio 2 (api/pos_catalogo.php), validados contra el PHP:
            // precio_compra 27.4, 20 unidades, 5% de ganancia, origen venezolano,
            // sin banderas de moneda visible (data_monedas solo trae claves bolivar).
            var calc = new CalculadoraPrecios(new ParametrosPrecios
            {
                PesoDolar = 4000,
                PesoBolivar = 0.0058,
                DolarBolivar = 1000,
                BolivarPeso = 0.0043,
                Bcv = 1000,
                PrecioDolarVisible = false,
                PrecioPesoVisible = false
            });
            var r = calc.Calcular(Producto(20, "v", 27.4, 5, ""));

            Iguales(new PreciosCalculados
            {
                PrecioVentaDolar = 1.44,
                PrecioVentaBs = 1438.5,
                PrecioVentaPeso = 6200
            }, r);
        }
    }
}
