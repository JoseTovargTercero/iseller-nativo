using System;
using System.Collections.Generic;
using System.IO;
using Iseller.Pos.Core.Carrito;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Modelos;
using Newtonsoft.Json;
using Xunit;

namespace Iseller.Pos.Tests
{
    public class CalculadoraFraccionadoTests
    {
        private static Dictionary<string, double> Pagos(double punto = 0, double bioPago = 0,
            double pagoMovil = 0, double transferencia = 0, double efectivo = 0,
            double dolares = 0, double pesos = 0)
        {
            return new Dictionary<string, double>
            {
                { CalculadoraFraccionado.Punto, punto },
                { CalculadoraFraccionado.BioPago, bioPago },
                { CalculadoraFraccionado.PagoMovil, pagoMovil },
                { CalculadoraFraccionado.Transferencia, transferencia },
                { CalculadoraFraccionado.Efectivo, efectivo },
                { CalculadoraFraccionado.Dolares, dolares },
                { CalculadoraFraccionado.Pesos, pesos },
            };
        }

        [Fact]
        public void Una_sola_moneda_cubre_la_cuenta()
        {
            var r = CalculadoraFraccionado.Calcular(Pagos(dolares: 10), 10, 40000, 4000);

            Assert.True(r.Saldado);
            Assert.Equal(0, r.RestanteUsd, 6);
            Assert.Equal(0, r.RestanteBs, 6);
            Assert.False(r.HayExceso);
        }

        [Fact]
        public void Varios_medios_cubren_por_fracciones_de_su_moneda()
        {
            // 2.000 de 4.000 Bs (50%) + 5 de 10 USD (50%) = cuenta cubierta.
            var r = CalculadoraFraccionado.Calcular(Pagos(efectivo: 2000, dolares: 5), 10, 40000, 4000);

            Assert.True(r.Saldado);
            Assert.Equal(1, r.Cobertura, 6);
        }

        [Fact]
        public void Pago_incompleto_devuelve_lo_que_falta_en_cada_moneda()
        {
            var r = CalculadoraFraccionado.Calcular(Pagos(dolares: 4), 10, 40000, 4000);

            Assert.False(r.Saldado);
            Assert.Equal(6, r.RestanteUsd, 6);
            Assert.Equal(24000, r.RestanteCop, 6);
            Assert.Equal(2400, r.RestanteBs, 6);
        }

        [Fact]
        public void El_restante_en_pesos_se_redondea_a_la_centena()
        {
            var r = CalculadoraFraccionado.Calcular(Pagos(dolares: 6.67), 10, 20200, 4000);

            Assert.False(r.Saldado);
            // falta = 0.333; 20200 * 0.333 = 6726.6 -> 6700
            Assert.Equal(6700, r.RestanteCop, 6);
        }

        [Fact]
        public void El_sobrepago_se_atribuye_a_una_sola_moneda()
        {
            var r = CalculadoraFraccionado.Calcular(Pagos(dolares: 12), 10, 40000, 4000);

            Assert.True(r.Saldado);
            Assert.True(r.HayExceso);
            Assert.Equal(2, r.ExcesoUsd, 6);
            Assert.Equal(0, r.ExcesoCop, 6);
            Assert.Equal(0, r.ExcesoBs, 6);
        }

        [Fact]
        public void El_vuelto_se_descuenta_del_efectivo_de_su_moneda()
        {
            // Cuenta de 2.000 Bs pagada con 2.500 en efectivo: 500 de vuelto.
            var r = CalculadoraFraccionado.Calcular(Pagos(efectivo: 2500), 10, 40000, 2000);
            Assert.True(r.Saldado);
            Assert.Equal(500, r.ExcesoBs, 6);

            var neto = CalculadoraFraccionado.Netos(r);
            Assert.Equal(2000, neto[CalculadoraFraccionado.Efectivo], 6);
        }

        [Fact]
        public void El_vuelto_de_pesos_se_descuenta_de_los_pesos()
        {
            var r = CalculadoraFraccionado.Calcular(Pagos(pesos: 50000), 10, 40000, 4000);
            Assert.True(r.Saldado);
            Assert.Equal(10000, r.ExcesoCop, 6);

            var neto = CalculadoraFraccionado.Netos(r);
            Assert.Equal(40000, neto[CalculadoraFraccionado.Pesos], 6);
        }

        [Fact]
        public void Redondea_a_la_centena_como_el_pos_web()
        {
            Assert.Equal(6700, CalculadoraFraccionado.RedondearCentena(6726.6), 6);
            Assert.Equal(6800, CalculadoraFraccionado.RedondearCentena(6750), 6);
            Assert.Equal(6700, CalculadoraFraccionado.RedondearCentena(6749.9), 6);
        }

        [Fact]
        public void Redondea_a_dos_decimales_como_el_pos_web()
        {
            Assert.Equal(1.01, CalculadoraFraccionado.Redondear2(1.005), 6);
            Assert.Equal(2.35, CalculadoraFraccionado.Redondear2(2.345), 6);
        }

        [Fact]
        public void Valida_el_desglose_obligatorio_del_pago_fraccionado()
        {
            var sinDesglose = new Venta { IdPedido = "uuid-1", MetodoPago = "option8", Despacho = 1 };
            sinDesglose.ProductosJson = JsonConvert.SerializeObject(new List<ItemCarrito>
            {
                new ItemCarrito { Id = 1, Name = "Arroz", Qty = 1, Price = 1, PricePeso = 1000, PriceBolivar = 100, CantidadPaca = 1 },
            });
            Assert.False(ValidadorVenta.Validar(sinDesglose).Valido);

            var enCero = new Venta { IdPedido = "uuid-2", MetodoPago = "option8", Despacho = 1 };
            enCero.ProductosJson = sinDesglose.ProductosJson;
            enCero.PagosJson = JsonConvert.SerializeObject(Pagos());
            Assert.False(ValidadorVenta.Validar(enCero).Valido);

            var negativo = new Venta { IdPedido = "uuid-3", MetodoPago = "option8", Despacho = 1 };
            negativo.ProductosJson = sinDesglose.ProductosJson;
            negativo.PagosJson = JsonConvert.SerializeObject(Pagos(efectivo: -5));
            Assert.False(ValidadorVenta.Validar(negativo).Valido);

            var valido = new Venta { IdPedido = "uuid-4", MetodoPago = "option8", Despacho = 1 };
            valido.ProductosJson = sinDesglose.ProductosJson;
            valido.PagosJson = JsonConvert.SerializeObject(Pagos(efectivo: 100));
            Assert.True(ValidadorVenta.Validar(valido).Valido);
        }

        [Fact]
        public void Una_venta_normal_no_exige_desglose()
        {
            var venta = new Venta { IdPedido = "uuid-5", MetodoPago = "option4", Despacho = 1 };
            venta.ProductosJson = JsonConvert.SerializeObject(new List<ItemCarrito>
            {
                new ItemCarrito { Id = 1, Name = "Arroz", Qty = 1, Price = 1, PricePeso = 1000, PriceBolivar = 100, CantidadPaca = 1 },
            });

            Assert.True(ValidadorVenta.Validar(venta).Valido);
        }
    }

    public class VentasRepositorioFraccionadoTests : IDisposable
    {
        private readonly string _ruta;

        public VentasRepositorioFraccionadoTests()
        {
            _ruta = Path.Combine(Path.GetTempPath(), "iseller-frac-" + Guid.NewGuid().ToString("N") + ".db");
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

        private static Venta VentaDePrueba(string id)
        {
            return new Venta
            {
                IdPedido = id,
                MetodoPago = "option8",
                Despacho = 1,
                UsuarioId = 22,
                UsuarioNombre = "CAJA NRO 1",
                CreadaEn = DateTimeOffset.Now,
                ProductosJson = JsonConvert.SerializeObject(new List<ItemCarrito>
                {
                    new ItemCarrito { Id = 1, Name = "Arroz", Qty = 1, Price = 1, PricePeso = 1000, PriceBolivar = 100, CantidadPaca = 1 },
                }),
                PagosJson = JsonConvert.SerializeObject(new Dictionary<string, double>
                {
                    { "punto", 500 },
                    { "efectivo", 1500 },
                    { "dolares", 1.25 },
                }),
            };
        }

        [Fact]
        public void Guarda_y_recupera_el_desglose_fraccionado()
        {
            var repo = new VentasRepositorio(_ruta);
            repo.Guardar(VentaDePrueba("uuid-frac-1"));

            var leida = repo.Obtener("uuid-frac-1");

            Assert.NotNull(leida);
            Assert.Equal("option8", leida.MetodoPago);
            var pagos = leida.PagosFraccionados();
            Assert.NotNull(pagos);
            Assert.Equal(500, pagos["punto"], 6);
            Assert.Equal(1500, pagos["efectivo"], 6);
            Assert.Equal(1.25, pagos["dolares"], 6);
        }

        [Fact]
        public void Agrega_la_columna_pagos_json_a_una_base_anterior()
        {
            // Base con el esquema viejo (sin pagos_json): la app debe migrarla al abrir.
            using (var conexion = new System.Data.SQLite.SQLiteConnection("Data Source=" + _ruta))
            {
                conexion.Open();
                using (var cmd = conexion.CreateCommand())
                {
                    cmd.CommandText =
                        "CREATE TABLE ventas (id_pedido TEXT PRIMARY KEY, fecha_local TEXT NOT NULL, " +
                        "metodo_pago TEXT NOT NULL, despacho INTEGER NOT NULL, total_usd REAL NOT NULL, " +
                        "total_bs REAL NOT NULL, total_cop REAL NOT NULL, cliente_cedula TEXT, " +
                        "cliente_nombre TEXT, cliente_telefono TEXT, usuario_id INTEGER NOT NULL, " +
                        "usuario_nombre TEXT NOT NULL, estado TEXT NOT NULL, intentos INTEGER NOT NULL DEFAULT 0, " +
                        "ultimo_error TEXT, tasas_json TEXT NOT NULL, productos_json TEXT NOT NULL, " +
                        "creada_en TEXT NOT NULL, enviada_en TEXT)";
                    cmd.ExecuteNonQuery();
                }
            }

            var repo = new VentasRepositorio(_ruta);
            repo.Guardar(VentaDePrueba("uuid-frac-2"));

            Assert.NotNull(repo.Obtener("uuid-frac-2").PagosFraccionados());
        }
    }
}
