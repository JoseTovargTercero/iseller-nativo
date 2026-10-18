using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Iseller.Pos.Core.Modelos;
using System.Data.SQLite;

namespace Iseller.Pos.Core.Datos
{
    /// <summary>
    /// Lectura/escritura del catÃ¡logo local. El catÃ¡logo llega como snapshot completo:
    /// se reemplaza entero dentro de una transacciÃ³n.
    /// </summary>
    public sealed class CatalogoRepositorio
    {
        private readonly string _ruta;

        public CatalogoRepositorio(string ruta = null)
        {
            _ruta = ruta ?? BasePos.RutaArchivo;
        }

        // â”€â”€ Escritura â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public void GuardarCatalogo(
            IEnumerable<Producto> productos,
            IEnumerable<Cliente> clientes,
            Tasas tasas,
            ConfiguracionNegocio configuracion,
            int sucursalId,
            string version,
            DateTimeOffset servidorEn)
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var tx = conexion.BeginTransaction())
            {
                Ejecutar(conexion, tx, "DELETE FROM productos; DELETE FROM stock; DELETE FROM combo_items; DELETE FROM clientes;");

                foreach (var p in productos)
                {
                    using (var cmd = conexion.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "INSERT INTO productos (id, nombre, nombre_busqueda, codigo_barras, origen, precio_compra, " +
                            "cantidad_unidades, porcentaje, mayor, es_combo, activo, foto, actualizado_en) " +
                            "VALUES (@id, @nombre, @busqueda, @codigo, @origen, @compra, @unidades, @porcentaje, @mayor, " +
                            "@combo, 1, @foto, @fecha)";
                        cmd.Parameters.AddWithValue("@id", p.Id);
                        cmd.Parameters.AddWithValue("@nombre", p.Nombre ?? "");
                        cmd.Parameters.AddWithValue("@busqueda", Normalizar(p.Nombre));
                        cmd.Parameters.AddWithValue("@codigo", (object)p.CodigoBarras ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@origen", p.Origen ?? "v");
                        cmd.Parameters.AddWithValue("@compra", p.PrecioCompra);
                        cmd.Parameters.AddWithValue("@unidades", p.CantidadUnidades);
                        cmd.Parameters.AddWithValue("@porcentaje", p.Porcentaje);
                        cmd.Parameters.AddWithValue("@mayor", p.Mayor ?? "0");
                        cmd.Parameters.AddWithValue("@combo", p.EsCombo);
                        cmd.Parameters.AddWithValue("@foto", (object)p.Foto ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@fecha", servidorEn.ToString("o"));
                        cmd.ExecuteNonQuery();
                    }

                    InsertarStock(conexion, tx, p.Id, sucursalId, p.Stock);

                    foreach (var item in p.ComboItems ?? new List<ItemCombo>())
                    {
                        using (var cmd = conexion.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText =
                                "INSERT OR REPLACE INTO combo_items (combo_id, nombre, cantidad) " +
                                "VALUES (@combo, @nombre, @cantidad)";
                            cmd.Parameters.AddWithValue("@combo", p.Id);
                            cmd.Parameters.AddWithValue("@nombre", item.Nombre ?? "");
                            cmd.Parameters.AddWithValue("@cantidad", item.Cantidad);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                foreach (var c in clientes)
                {
                    using (var cmd = conexion.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "INSERT OR REPLACE INTO clientes (cedula, nombre, telefono) VALUES (@cedula, @nombre, @telefono)";
                        cmd.Parameters.AddWithValue("@cedula", c.Cedula ?? "");
                        cmd.Parameters.AddWithValue("@nombre", c.Nombre ?? "");
                        cmd.Parameters.AddWithValue("@telefono", c.Telefono ?? "");
                        cmd.ExecuteNonQuery();
                    }
                }

                GuardarTasas(conexion, tx, tasas);

                SetMeta(conexion, tx, "catalogo_version", version ?? "");
                SetMeta(conexion, tx, "catalogo_servidor_en", servidorEn.ToString("o"));
                SetMeta(conexion, tx, "sucursal_id", sucursalId.ToString(CultureInfo.InvariantCulture));
                SetMeta(conexion, tx, "config_tickets", configuracion.Tickets.ToString(CultureInfo.InvariantCulture));
                SetMeta(conexion, tx, "config_bs_ticket", configuracion.BsTicket.ToString(CultureInfo.InvariantCulture));
                SetMeta(conexion, tx, "config_cortes_caja", configuracion.CortesCaja.ToString(CultureInfo.InvariantCulture));
                SetMeta(conexion, tx, "config_registro_clientes", configuracion.RegistroClientes.ToString(CultureInfo.InvariantCulture));

                tx.Commit();
            }
        }

        public void GuardarTasas(Tasas tasas)
        {
            if (tasas == null)
            {
                return;
            }
            using (var conexion = BasePos.Abrir(_ruta))
            using (var tx = conexion.BeginTransaction())
            {
                GuardarTasas(conexion, tx, tasas);
                tx.Commit();
            }
        }

        private static void GuardarTasas(SQLiteConnection conexion, SQLiteTransaction tx, Tasas tasas)
        {
            if (tasas == null)
            {
                return;
            }
            using (var cmd = conexion.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    "INSERT OR REPLACE INTO tasas (id, peso_dolar, peso_bolivar, dolar_bolivar, bolivar_peso, bcv, " +
                    "precio_dolar_visible, precio_peso_visible, actualizado_en) " +
                    "VALUES (1, @pd, @pb, @db, @bp, @bcv, @dolarVis, @pesoVis, @fecha)";
                cmd.Parameters.AddWithValue("@pd", tasas.PesoDolar);
                cmd.Parameters.AddWithValue("@pb", tasas.PesoBolivar);
                cmd.Parameters.AddWithValue("@db", tasas.DolarBolivar);
                cmd.Parameters.AddWithValue("@bp", tasas.BolivarPeso);
                cmd.Parameters.AddWithValue("@bcv", tasas.Bcv);
                cmd.Parameters.AddWithValue("@dolarVis", tasas.PrecioDolarVisible ? 1 : 0);
                cmd.Parameters.AddWithValue("@pesoVis", tasas.PrecioPesoVisible ? 1 : 0);
                cmd.Parameters.AddWithValue("@fecha",
                    tasas.ActualizadoEn.HasValue ? tasas.ActualizadoEn.Value.ToString("o") : DateTimeOffset.Now.ToString("o"));
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertarStock(SQLiteConnection conexion, SQLiteTransaction tx, int productoId, int sucursalId, double cantidad)
        {
            using (var cmd = conexion.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    "INSERT OR REPLACE INTO stock (producto_id, sucursal_id, cantidad) VALUES (@producto, @sucursal, @cantidad)";
                cmd.Parameters.AddWithValue("@producto", productoId);
                cmd.Parameters.AddWithValue("@sucursal", sucursalId);
                cmd.Parameters.AddWithValue("@cantidad", cantidad);
                cmd.ExecuteNonQuery();
            }
        }

        // â”€â”€ Lectura â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public string Version()
        {
            return Meta("catalogo_version") ?? "";
        }

        public DateTimeOffset? ServidorEn()
        {
            return LeerFecha(Meta("catalogo_servidor_en"));
        }

        public int SucursalId()
        {
            int valor;
            return int.TryParse(Meta("sucursal_id"), out valor) ? valor : 0;
        }

        public ConfiguracionNegocio ObtenerConfiguracion()
        {
            return new ConfiguracionNegocio
            {
                Tickets = Entero("config_tickets"),
                BsTicket = Entero("config_bs_ticket"),
                CortesCaja = Entero("config_cortes_caja"),
                RegistroClientes = Entero("config_registro_clientes"),
            };
        }

        public Tasas ObtenerTasas()
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT peso_dolar, peso_bolivar, dolar_bolivar, bolivar_peso, bcv, " +
                    "precio_dolar_visible, precio_peso_visible, actualizado_en FROM tasas WHERE id = 1";
                using (var lector = cmd.ExecuteReader())
                {
                    if (!lector.Read())
                    {
                        return null;
                    }
                    return new Tasas
                    {
                        PesoDolar = lector.GetDouble(0),
                        PesoBolivar = lector.GetDouble(1),
                        DolarBolivar = lector.GetDouble(2),
                        BolivarPeso = lector.GetDouble(3),
                        Bcv = lector.GetDouble(4),
                        PrecioDolarVisible = lector.GetInt64(5) == 1,
                        PrecioPesoVisible = lector.GetInt64(6) == 1,
                        ActualizadoEn = LeerFecha(lector.IsDBNull(7) ? null : lector.GetString(7)),
                    };
                }
            }
        }

        public int ContarProductos()
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM productos";
                return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        public List<Producto> Productos()
        {
            return ConsultarProductos(
                "SELECT p.id, p.nombre, p.codigo_barras, p.origen, p.precio_compra, p.cantidad_unidades, " +
                "p.porcentaje, p.mayor, p.es_combo, p.foto, COALESCE(s.cantidad, 0) " +
                "FROM productos p LEFT JOIN stock s ON s.producto_id = p.id " +
                "WHERE p.id IN (SELECT producto_id FROM stock WHERE sucursal_id = (SELECT valor FROM meta WHERE clave = 'sucursal_id'))",
                null);
        }

        /// <summary>BÃºsqueda por nombre (sin acentos) y por cÃ³digo de barras.</summary>
        public List<Producto> Buscar(string termino, int limite = 50)
        {
            string limpio = (termino ?? "").Trim();
            if (limpio.Length == 0)
            {
                return new List<Producto>();
            }

            string normalizado = Normalizar(limpio);
            if (normalizado.Length == 0)
            {
                return new List<Producto>();
            }
            string soloDigitos = new string(limpio.Where(char.IsDigit).ToArray());

            string sql =
                "SELECT p.id, p.nombre, p.codigo_barras, p.origen, p.precio_compra, p.cantidad_unidades, " +
                "p.porcentaje, p.mayor, p.es_combo, p.foto, COALESCE(s.cantidad, 0) " +
                "FROM productos p LEFT JOIN stock s ON s.producto_id = p.id " +
                "WHERE p.nombre_busqueda LIKE @termino ESCAPE '\\' " +
                "OR (@digitos <> '' AND p.codigo_barras LIKE @codigo ESCAPE '\\') " +
                "ORDER BY CASE WHEN p.codigo_barras = @exacto THEN 0 ELSE 1 END, p.nombre " +
                "LIMIT " + limite.ToString(CultureInfo.InvariantCulture);

            return ConsultarProductos(sql, cmd =>
            {
                cmd.Parameters.AddWithValue("@termino", "%" + Escapar(normalizado) + "%");
                cmd.Parameters.AddWithValue("@digitos", soloDigitos);
                cmd.Parameters.AddWithValue("@codigo", "%" + Escapar(soloDigitos) + "%");
                cmd.Parameters.AddWithValue("@exacto", limpio);
            });
        }

        public Producto BuscarPorCodigo(string codigo)
        {
            string limpio = (codigo ?? "").Trim();
            if (limpio.Length == 0)
            {
                return null;
            }
            var lista = ConsultarProductos(
                "SELECT p.id, p.nombre, p.codigo_barras, p.origen, p.precio_compra, p.cantidad_unidades, " +
                "p.porcentaje, p.mayor, p.es_combo, p.foto, COALESCE(s.cantidad, 0) " +
                "FROM productos p LEFT JOIN stock s ON s.producto_id = p.id " +
                "WHERE p.codigo_barras = @codigo LIMIT 1",
                cmd => cmd.Parameters.AddWithValue("@codigo", limpio));
            return lista.FirstOrDefault();
        }

        public Cliente ObtenerCliente(string cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula))
            {
                return null;
            }
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "SELECT cedula, nombre, telefono FROM clientes WHERE cedula = @cedula LIMIT 1";
                cmd.Parameters.AddWithValue("@cedula", cedula.Trim());
                using (var lector = cmd.ExecuteReader())
                {
                    if (!lector.Read())
                    {
                        return null;
                    }
                    return new Cliente
                    {
                        Cedula = lector.GetString(0),
                        Nombre = lector.IsDBNull(1) ? "" : lector.GetString(1),
                        Telefono = lector.IsDBNull(2) ? "" : lector.GetString(2),
                    };
                }
            }
        }

        private List<Producto> ConsultarProductos(string sql, Action<SQLiteCommand> parametros)
        {
            var resultado = new List<Producto>();
            using (var conexion = BasePos.Abrir(_ruta))
            {
                var combos = new Dictionary<int, List<ItemCombo>>();
                using (var cmdCombo = conexion.CreateCommand())
                {
                    cmdCombo.CommandText = "SELECT combo_id, nombre, cantidad FROM combo_items";
                    using (var lector = cmdCombo.ExecuteReader())
                    {
                        while (lector.Read())
                        {
                            int comboId = lector.GetInt32(0);
                            List<ItemCombo> items;
                            if (!combos.TryGetValue(comboId, out items))
                            {
                                items = new List<ItemCombo>();
                                combos[comboId] = items;
                            }
                            items.Add(new ItemCombo
                            {
                                Nombre = lector.IsDBNull(1) ? "" : lector.GetString(1),
                                Cantidad = lector.GetDouble(2),
                            });
                        }
                    }
                }

                using (var cmd = conexion.CreateCommand())
                {
                    cmd.CommandText = sql;
                    if (parametros != null)
                    {
                        parametros(cmd);
                    }
                    using (var lector = cmd.ExecuteReader())
                    {
                        while (lector.Read())
                        {
                            int id = lector.GetInt32(0);
                            List<ItemCombo> items;
                            combos.TryGetValue(id, out items);
                            resultado.Add(new Producto
                            {
                                Id = id,
                                Nombre = lector.GetString(1),
                                CodigoBarras = lector.IsDBNull(2) ? "" : lector.GetString(2),
                                Origen = lector.IsDBNull(3) ? "v" : lector.GetString(3),
                                PrecioCompra = lector.GetDouble(4),
                                CantidadUnidades = lector.GetDouble(5),
                                Porcentaje = lector.GetDouble(6),
                                Mayor = lector.IsDBNull(7) ? "0" : lector.GetString(7),
                                EsCombo = lector.GetInt32(8),
                                Foto = lector.IsDBNull(9) ? "" : lector.GetString(9),
                                Stock = lector.GetDouble(10),
                                ComboItems = items ?? new List<ItemCombo>(),
                            });
                        }
                    }
                }
            }
            return resultado;
        }

        // â”€â”€ Auxiliares â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>MayÃºsculas y sin acentos, como la bÃºsqueda del POS web.</summary>
        public static string Normalizar(string texto)
        {
            if (string.IsNullOrEmpty(texto))
            {
                return "";
            }
            string descompuesto = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);
            foreach (char c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                {
                    sb.Append(char.ToUpperInvariant(c));
                }
                else
                {
                    sb.Append(' ');
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string Escapar(string valor)
        {
            return valor.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        }

        private static void Ejecutar(SQLiteConnection conexion, SQLiteTransaction tx, string sql)
        {
            using (var cmd = conexion.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private string Meta(string clave)
        {
            using (var conexion = BasePos.Abrir(_ruta))
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "SELECT valor FROM meta WHERE clave = @clave LIMIT 1";
                cmd.Parameters.AddWithValue("@clave", clave);
                var valor = cmd.ExecuteScalar();
                return valor == null || valor == DBNull.Value ? null : Convert.ToString(valor, CultureInfo.InvariantCulture);
            }
        }

        private static void SetMeta(SQLiteConnection conexion, SQLiteTransaction tx, string clave, string valor)
        {
            using (var cmd = conexion.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT OR REPLACE INTO meta (clave, valor) VALUES (@clave, @valor)";
                cmd.Parameters.AddWithValue("@clave", clave);
                cmd.Parameters.AddWithValue("@valor", valor ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private int Entero(string clave)
        {
            int valor;
            return int.TryParse(Meta(clave), out valor) ? valor : 0;
        }

        private static DateTimeOffset? LeerFecha(string texto)
        {
            DateTimeOffset valor;
            return DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out valor)
                ? (DateTimeOffset?)valor
                : null;
        }
    }
}
