using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.Core.Datos
{
    /// <summary>Carritos reservados en la terminal.</summary>
    public sealed class ReservadosRepositorio
    {
        private readonly string _ruta;

        public ReservadosRepositorio(string ruta = null)
        {
            _ruta = ruta ?? BasePos.RutaArchivo;
        }

        public void Guardar(Reservado reservado)
        {
            if (reservado == null)
            {
                throw new ArgumentNullException(nameof(reservado));
            }

            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT OR REPLACE INTO carritos_reservados (id, cliente, productos_json, creada_en) " +
                    "VALUES (@id, @cliente, @productos, @creada)";
                cmd.Parameters.AddWithValue("@id", reservado.Id);
                cmd.Parameters.AddWithValue("@cliente", reservado.Cliente ?? "");
                cmd.Parameters.AddWithValue("@productos", reservado.ProductosJson ?? "[]");
                cmd.Parameters.AddWithValue("@creada", reservado.CreadaEn.ToString("o"));
                cmd.ExecuteNonQuery();
            }
        }

        public List<Reservado> Listar()
        {
            var lista = new List<Reservado>();
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT id, cliente, productos_json, creada_en FROM carritos_reservados ORDER BY creada_en DESC";
                using (var lector = cmd.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        lista.Add(new Reservado
                        {
                            Id = lector.GetString(0),
                            Cliente = lector.IsDBNull(1) ? "" : lector.GetString(1),
                            ProductosJson = lector.IsDBNull(2) ? "[]" : lector.GetString(2),
                            CreadaEn = Fecha(lector, 3),
                        });
                    }
                }
            }
            return lista;
        }

        public Reservado Obtener(string id)
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT id, cliente, productos_json, creada_en FROM carritos_reservados WHERE id = @id LIMIT 1";
                cmd.Parameters.AddWithValue("@id", id ?? "");
                using (var lector = cmd.ExecuteReader())
                {
                    if (!lector.Read())
                    {
                        return null;
                    }
                    return new Reservado
                    {
                        Id = lector.GetString(0),
                        Cliente = lector.IsDBNull(1) ? "" : lector.GetString(1),
                        ProductosJson = lector.IsDBNull(2) ? "[]" : lector.GetString(2),
                        CreadaEn = Fecha(lector, 3),
                    };
                }
            }
        }

        public void Eliminar(string id)
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM carritos_reservados WHERE id = @id";
                cmd.Parameters.AddWithValue("@id", id ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        public int Contar()
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM carritos_reservados";
                return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
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
