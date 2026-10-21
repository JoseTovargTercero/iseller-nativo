using System;
using System.Data.SQLite;
using System.IO;
using System.Reflection;

namespace Iseller.Pos.Core.Datos
{
    /// <summary>Ubicación y apertura de la base local de la caja.</summary>
    public static class BasePos
    {
        public static string Directorio
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "iSeller");
            }
        }

        public static string RutaArchivo
        {
            get { return Path.Combine(Directorio, "pos.db"); }
        }

        /// <summary>Abre (y crea si falta) la base, con el esquema aplicado.</summary>
        public static SQLiteConnection Abrir(string ruta = null)
        {
            string destino = ruta ?? RutaArchivo;
            string carpeta = Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(carpeta) && !Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }

            var cadena = new SQLiteConnectionStringBuilder
            {
                DataSource = destino,
                Version = 3,
            }.ToString();

            var conexion = new SQLiteConnection(cadena);
            conexion.Open();

            using (var pragma = conexion.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
                pragma.ExecuteNonQuery();
            }

            AplicarEsquema(conexion);
            return conexion;
        }

        private static void AplicarEsquema(SQLiteConnection conexion)
        {
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = LeerEsquema();
                cmd.ExecuteNonQuery();
            }

            // Migraciones aditivas para bases ya creadas (el CREATE TABLE IF NOT EXISTS
            // no agrega columnas nuevas).
            AgregarColumnaSiFalta(conexion, "ventas", "pagos_json", "TEXT");
        }

        private static void AgregarColumnaSiFalta(SQLiteConnection conexion, string tabla, string columna, string tipo)
        {
            bool existe = false;
            using (var cmd = conexion.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(" + tabla + ")";
                using (var lector = cmd.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        if (string.Equals(lector.GetString(1), columna, StringComparison.OrdinalIgnoreCase))
                        {
                            existe = true;
                            break;
                        }
                    }
                }
            }

            if (!existe)
            {
                using (var cmd = conexion.CreateCommand())
                {
                    cmd.CommandText = "ALTER TABLE " + tabla + " ADD COLUMN " + columna + " " + tipo;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static string LeerEsquema()
        {
            var ensamblado = typeof(BasePos).Assembly;
            string recurso = null;
            foreach (var nombre in ensamblado.GetManifestResourceNames())
            {
                if (nombre.EndsWith("esquema.sql", StringComparison.OrdinalIgnoreCase))
                {
                    recurso = nombre;
                    break;
                }
            }
            if (recurso == null)
            {
                throw new InvalidOperationException("No se encontró db/esquema.sql embebido.");
            }

            using (var stream = ensamblado.GetManifestResourceStream(recurso))
            using (var lector = new StreamReader(stream))
            {
                return lector.ReadToEnd();
            }
        }
    }
}
