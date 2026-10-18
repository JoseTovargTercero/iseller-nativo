using System;
using System.Windows;
using Iseller.Pos.Core.Auth;
using Iseller.Pos.Core.Configuracion;

namespace Iseller.Pos.App
{
    public partial class App : Application
    {
        /// <summary>Versión publicada en api/pos_version.json.</summary>
        public const string VersionApp = "1.0.0";

        public AppConfig Config { get; private set; }
        public AlmacenCredenciales Credenciales { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (s, args) => RegistrarError(args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, args) => RegistrarError(args.ExceptionObject as Exception);

            Config = AppConfig.Cargar();
            Credenciales = new AlmacenCredenciales();

            var sesionGuardada = Credenciales.Cargar();
            var acceso = new LoginWindow(Config, Credenciales, sesionGuardada);
            bool? entro = acceso.ShowDialog();

            if (entro == true && acceso.Sesion != null)
            {
                var principal = new MainWindow(Config, acceso.Sesion);
                principal.Closed += (s, args) => Shutdown();
                principal.Show();
            }
            else
            {
                Shutdown();
            }
        }

        /// <summary>Deja rastro de cualquier fallo inesperado para poder diagnosticarlo en la caja.</summary>
        private static void RegistrarError(Exception ex)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Iseller.Pos.Core.Datos.BasePos.Directorio);
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(Iseller.Pos.Core.Datos.BasePos.Directorio, "error.log"),
                    DateTime.Now.ToString("u") + "  " + (ex == null ? "(sin detalle)" : ex.ToString()) +
                    Environment.NewLine + Environment.NewLine);
            }
            catch (Exception)
            {
                // Si ni siquiera se puede escribir el log, no queda nada más que hacer.
            }
        }
    }
}
