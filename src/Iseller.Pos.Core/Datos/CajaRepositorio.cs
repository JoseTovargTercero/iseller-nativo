using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using Iseller.Pos.Core.Modelos;
using Newtonsoft.Json;

namespace Iseller.Pos.Core.Datos
{
    /// <summary>Aperturas y cierres locales, pendientes de sincronizar.</summary>
    public sealed class CajaRepositorio
    {
        private readonly string _ruta;

        public CajaRepositorio(string ruta = null)
        {
            _ruta = ruta ?? BasePos.RutaArchivo;
        }

        public void Guardar(RegistroCaja registro)
        {
            if (registro == null)
            {
                throw new ArgumentNullException(nameof(registro));
            }

            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT OR REPLACE INTO caja (id, tipo, montos_json, observaciones, usuario_id, creada_en, sincronizada_en) " +
                    "VALUES (@id, @tipo, @montos, @obs, @usuario, @creada, @sincronizada)";
                cmd.Parameters.AddWithValue("@id", registro.Id);
                cmd.Parameters.AddWithValue("@tipo", registro.Tipo);
                cmd.Parameters.AddWithValue("@montos", JsonConvert.SerializeObject(registro.Montos ?? new Dictionary<string, double>()));
                cmd.Parameters.AddWithValue("@obs", registro.Observaciones ?? "");
                cmd.Parameters.AddWithValue("@usuario", registro.UsuarioId);
                cmd.Parameters.AddWithValue("@creada", registro.CreadaEn.ToString("o"));
                cmd.Parameters.AddWithValue("@sincronizada",
                    registro.SincronizadaEn.HasValue ? (object)registro.SincronizadaEn.Value.ToString("o") : DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        public List<RegistroCaja> Pendientes()
        {
            return Consultar("WHERE sincronizada_en IS NULL ORDER BY creada_en");
        }

        public int ContarPendientes()
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM caja WHERE sincronizada_en IS NULL";
                return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        public void MarcarSincronizada(string id, DateTimeOffset cuando)
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "UPDATE caja SET sincronizada_en = @cuando WHERE id = @id";
                cmd.Parameters.AddWithValue("@cuando", cuando.ToString("o"));
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public void Eliminar(string id)
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM caja WHERE id = @id";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Último movimiento del día para ese usuario (apertura o cierre).</summary>
        public RegistroCaja UltimoDelDia(int usuarioId, DateTime fechaLocal)
        {
            string fecha = fechaLocal.ToString("yyyy-MM-dd");
            var lista = Consultar(
                "WHERE usuario_id = @usuario AND substr(creada_en, 1, 10) = @fecha ORDER BY creada_en DESC LIMIT 1",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@usuario", usuarioId);
                    cmd.Parameters.AddWithValue("@fecha", fecha);
                });
            return lista.Count > 0 ? lista[0] : null;
        }

        public EstadoCaja Estado(int usuarioId, DateTime fechaLocal)
        {
            var ultimo = UltimoDelDia(usuarioId, fechaLocal);
            if (ultimo == null)
            {
                return EstadoCaja.SinApertura;
            }
            return ultimo.EsCierre ? EstadoCaja.Cerrada : EstadoCaja.Abierta;
        }

        private List<RegistroCaja> Consultar(string filtro, Action<SQLiteCommand> parametros = null)
        {
            var registros = new List<RegistroCaja>();
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT id, tipo, montos_json, observaciones, usuario_id, creada_en, sincronizada_en " +
                    "FROM caja " + filtro;
                if (parametros != null)
                {
                    parametros(cmd);
                }
                using (var lector = cmd.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        registros.Add(new RegistroCaja
                        {
                            Id = lector.GetString(0),
                            Tipo = lector.GetString(1),
                            Montos = LeerMontos(lector.IsDBNull(2) ? "{}" : lector.GetString(2)),
                            Observaciones = lector.IsDBNull(3) ? "" : lector.GetString(3),
                            UsuarioId = lector.GetInt32(4),
                            CreadaEn = Fecha(lector, 5),
                            SincronizadaEn = lector.IsDBNull(6) ? (DateTimeOffset?)null : Fecha(lector, 6),
                        });
                    }
                }
            }
            return registros;
        }

        private static Dictionary<string, double> LeerMontos(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, double>>(json)
                       ?? new Dictionary<string, double>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, double>();
            }
        }

        private static DateTimeOffset Fecha(SQLiteDataReader lector, int indice)
        {
            DateTimeOffset valor;
            return DateTimeOffset.TryParse(lector.GetString(indice), CultureInfo.InvariantCulture,
                DateTimeStyles.None, out valor) ? valor : DateTimeOffset.MinValue;
        }
    }
}
