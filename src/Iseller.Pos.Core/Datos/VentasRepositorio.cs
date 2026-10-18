using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.Core.Datos
{
    /// <summary>Ventas guardadas en la caja (pendientes de sincronizar o ya enviadas).</summary>
    public sealed class VentasRepositorio
    {
        private const string Columnas =
            "id_pedido, fecha_local, metodo_pago, despacho, total_usd, total_bs, total_cop, " +
            "cliente_cedula, cliente_nombre, cliente_telefono, usuario_id, usuario_nombre, " +
            "estado, intentos, ultimo_error, tasas_json, productos_json, creada_en, enviada_en";

        private readonly string _ruta;

        public VentasRepositorio(string ruta = null)
        {
            _ruta = ruta ?? BasePos.RutaArchivo;
        }

        public void Guardar(Venta venta)
        {
            if (venta == null)
            {
                throw new ArgumentNullException(nameof(venta));
            }

            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT OR REPLACE INTO ventas (" + Columnas + ") VALUES (" +
                    "@id_pedido, @fecha_local, @metodo_pago, @despacho, @total_usd, @total_bs, @total_cop, " +
                    "@cliente_cedula, @cliente_nombre, @cliente_telefono, @usuario_id, @usuario_nombre, " +
                    "@estado, @intentos, @ultimo_error, @tasas_json, @productos_json, @creada_en, @enviada_en)";
                cmd.Parameters.AddWithValue("@id_pedido", venta.IdPedido);
                cmd.Parameters.AddWithValue("@fecha_local", venta.FechaLocal.ToString("o"));
                cmd.Parameters.AddWithValue("@metodo_pago", venta.MetodoPago);
                cmd.Parameters.AddWithValue("@despacho", venta.Despacho);
                cmd.Parameters.AddWithValue("@total_usd", venta.TotalUsd);
                cmd.Parameters.AddWithValue("@total_bs", venta.TotalBs);
                cmd.Parameters.AddWithValue("@total_cop", venta.TotalCop);
                cmd.Parameters.AddWithValue("@cliente_cedula", venta.ClienteCedula ?? "");
                cmd.Parameters.AddWithValue("@cliente_nombre", venta.ClienteNombre ?? "");
                cmd.Parameters.AddWithValue("@cliente_telefono", venta.ClienteTelefono ?? "");
                cmd.Parameters.AddWithValue("@usuario_id", venta.UsuarioId);
                cmd.Parameters.AddWithValue("@usuario_nombre", venta.UsuarioNombre ?? "");
                cmd.Parameters.AddWithValue("@estado", venta.Estado ?? EstadoVenta.Pendiente);
                cmd.Parameters.AddWithValue("@intentos", venta.Intentos);
                cmd.Parameters.AddWithValue("@ultimo_error", (object)venta.UltimoError ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tasas_json", venta.TasasJson ?? "{}");
                cmd.Parameters.AddWithValue("@productos_json", venta.ProductosJson ?? "[]");
                cmd.Parameters.AddWithValue("@creada_en", venta.CreadaEn.ToString("o"));
                cmd.Parameters.AddWithValue("@enviada_en",
                    venta.EnviadaEn.HasValue ? (object)venta.EnviadaEn.Value.ToString("o") : DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        public Venta Obtener(string idPedido)
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "SELECT " + Columnas + " FROM ventas WHERE id_pedido = @id LIMIT 1";
                cmd.Parameters.AddWithValue("@id", idPedido ?? "");
                using (var lector = cmd.ExecuteReader())
                {
                    return lector.Read() ? Leer(lector) : null;
                }
            }
        }

        public List<Venta> Pendientes()
        {
            return Consultar("WHERE estado = @estado ORDER BY creada_en", cmd =>
                cmd.Parameters.AddWithValue("@estado", EstadoVenta.Pendiente));
        }

        public int ContarPendientes()
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM ventas WHERE estado = @estado";
                cmd.Parameters.AddWithValue("@estado", EstadoVenta.Pendiente);
                return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Ventas creadas entre dos instantes (para el listado del día).</summary>
        public List<Venta> Entre(DateTimeOffset desde, DateTimeOffset hasta)
        {
            return Consultar("WHERE creada_en >= @desde AND creada_en <= @hasta ORDER BY creada_en", cmd =>
            {
                cmd.Parameters.AddWithValue("@desde", desde.ToString("o"));
                cmd.Parameters.AddWithValue("@hasta", hasta.ToString("o"));
            });
        }

        private List<Venta> Consultar(string filtro, Action<SQLiteCommand> parametros)
        {
            var ventas = new List<Venta>();
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "SELECT " + Columnas + " FROM ventas " + filtro;
                if (parametros != null)
                {
                    parametros(cmd);
                }
                using (var lector = cmd.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        ventas.Add(Leer(lector));
                    }
                }
            }
            return ventas;
        }

        private static Venta Leer(SQLiteDataReader lector)
        {
            return new Venta
            {
                IdPedido = lector.GetString(0),
                FechaLocal = Fecha(lector, 1),
                MetodoPago = lector.IsDBNull(2) ? "" : lector.GetString(2),
                Despacho = lector.GetInt32(3),
                TotalUsd = lector.GetDouble(4),
                TotalBs = lector.GetDouble(5),
                TotalCop = lector.GetDouble(6),
                ClienteCedula = lector.IsDBNull(7) ? "" : lector.GetString(7),
                ClienteNombre = lector.IsDBNull(8) ? "" : lector.GetString(8),
                ClienteTelefono = lector.IsDBNull(9) ? "" : lector.GetString(9),
                UsuarioId = lector.GetInt32(10),
                UsuarioNombre = lector.IsDBNull(11) ? "" : lector.GetString(11),
                Estado = lector.IsDBNull(12) ? EstadoVenta.Pendiente : lector.GetString(12),
                Intentos = lector.GetInt32(13),
                UltimoError = lector.IsDBNull(14) ? null : lector.GetString(14),
                TasasJson = lector.IsDBNull(15) ? "{}" : lector.GetString(15),
                ProductosJson = lector.IsDBNull(16) ? "[]" : lector.GetString(16),
                CreadaEn = Fecha(lector, 17),
                EnviadaEn = lector.IsDBNull(18) ? (DateTimeOffset?)null : Fecha(lector, 18),
            };
        }

        private static DateTimeOffset Fecha(SQLiteDataReader lector, int indice)
        {
            if (lector.IsDBNull(indice))
            {
                return DateTimeOffset.MinValue;
            }
            DateTimeOffset valor;
            return DateTimeOffset.TryParse(lector.GetString(indice), CultureInfo.InvariantCulture,
                DateTimeStyles.None, out valor) ? valor : DateTimeOffset.MinValue;
        }
    }
}
