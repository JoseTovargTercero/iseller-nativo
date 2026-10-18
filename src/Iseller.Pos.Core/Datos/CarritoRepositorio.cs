using System;
using System.Collections.Generic;
using Iseller.Pos.Core.Modelos;
using Newtonsoft.Json;
using System.Data.SQLite;

namespace Iseller.Pos.Core.Datos
{
    /// <summary>Carrito en curso, guardado para sobrevivir a un cierre de la app.</summary>
    public sealed class CarritoRepositorio
    {
        private readonly string _ruta;

        public CarritoRepositorio(string ruta = null)
        {
            _ruta = ruta ?? BasePos.RutaArchivo;
        }

        public List<ItemCarrito> Cargar()
        {
            var items = new List<ItemCarrito>();
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "SELECT item_json FROM carrito_activo ORDER BY agregado_en";
                using (var lector = cmd.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        try
                        {
                            var item = JsonConvert.DeserializeObject<ItemCarrito>(lector.GetString(0));
                            if (item != null && item.Qty > 0)
                            {
                                items.Add(item);
                            }
                        }
                        catch (JsonException)
                        {
                            // Fila corrupta: se ignora en lugar de romper el arranque.
                        }
                    }
                }
            }
            return items;
        }

        public void GuardarItem(ItemCarrito item)
        {
            if (item == null)
            {
                return;
            }
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT OR REPLACE INTO carrito_activo (producto_id, item_json, agregado_en) VALUES (@id, @json, @fecha)";
                cmd.Parameters.AddWithValue("@id", item.Id);
                cmd.Parameters.AddWithValue("@json", JsonConvert.SerializeObject(item));
                cmd.Parameters.AddWithValue("@fecha", DateTimeOffset.Now.ToString("o"));
                cmd.ExecuteNonQuery();
            }
        }

        public void Quitar(int productoId)
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM carrito_activo WHERE producto_id = @id";
                cmd.Parameters.AddWithValue("@id", productoId);
                cmd.ExecuteNonQuery();
            }
        }

        public void Vaciar()
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM carrito_activo";
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Deja el carrito en disco exactamente como está en memoria.</summary>
        public void Reemplazar(IEnumerable<ItemCarrito> items)
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var tx = conexion.BeginTransaction())
            {
                using (var borrar = conexion.CreateCommand())
                {
                    borrar.Transaction = tx;
                    borrar.CommandText = "DELETE FROM carrito_activo";
                    borrar.ExecuteNonQuery();
                }

                foreach (var item in items ?? new List<ItemCarrito>())
                {
                    using (var cmd = conexion.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "INSERT OR REPLACE INTO carrito_activo (producto_id, item_json, agregado_en) VALUES (@id, @json, @fecha)";
                        cmd.Parameters.AddWithValue("@id", item.Id);
                        cmd.Parameters.AddWithValue("@json", JsonConvert.SerializeObject(item));
                        cmd.Parameters.AddWithValue("@fecha", DateTimeOffset.Now.ToString("o"));
                        cmd.ExecuteNonQuery();
                    }
                }

                tx.Commit();
            }
        }
    }
}
