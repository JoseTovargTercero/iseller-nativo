using System;
using System.Windows;
using System.Windows.Input;
using Iseller.Pos.Core.Api;
using Iseller.Pos.Core.Auth;
using Iseller.Pos.Core.Configuracion;
using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.App
{
    public partial class LoginWindow : Window
    {
        private readonly AppConfig _config;
        private readonly AlmacenCredenciales _credenciales;
        private int? _sucursalElegida;

        public SesionLocal Sesion { get; private set; }

        public LoginWindow(AppConfig config, AlmacenCredenciales credenciales)
        {
            InitializeComponent();

            _config = config;
            _credenciales = credenciales;

            Estado(_credenciales.CargarTodas().Count > 0
                ? "Si no hay conexión, puedes entrar con tu contraseña: este equipo recuerda a los usuarios que ya iniciaron sesión."
                : "Primer acceso: se necesita conexión a internet.");

            Loaded += (s, e) => CampoUsuario.Focus();
        }

        private async void BotonEntrar_Click(object sender, RoutedEventArgs e)
        {
            string usuario = CampoUsuario.Text.Trim();
            string clave = CampoClave.Password;

            if (usuario.Length == 0 || clave.Length == 0)
            {
                Estado("Escribe el usuario y la contraseña.");
                return;
            }

            BotonEntrar.IsEnabled = false;
            Estado("Conectando…");
            _sucursalElegida = ComboSucursal.SelectedValue is int elegida ? elegida : (int?)null;

            try
            {
                using (var api = new PosApiClient(_config.ApiBaseUrl, _config.ApiKey))
                {
                    var resultado = await api.LoginAsync(usuario, clave, _sucursalElegida);

                    if (resultado.RequiereSucursal)
                    {
                        ComboSucursal.ItemsSource = resultado.Sucursales;
                        PanelSucursal.Visibility = Visibility.Visible;
                        if (ComboSucursal.SelectedIndex < 0 && resultado.Sucursales.Count > 0)
                        {
                            ComboSucursal.SelectedIndex = 0;
                        }
                        Estado("Este usuario tiene varias sucursales. Elige una y vuelve a pulsar Entrar.");
                        return;
                    }

                    if (resultado.MembresiaVencida)
                    {
                        Estado("La membresía del negocio está vencida. Renueva para poder vender.");
                        return;
                    }

                    if (!resultado.Ok || resultado.Sesion == null)
                    {
                        Estado(resultado.Error ?? "No se pudo iniciar sesión.");
                        return;
                    }

                    string sal;
                    string hash;
                    ClaveLocal.Hashear(clave, out sal, out hash);
                    resultado.Sesion.ClaveSal = sal;
                    resultado.Sesion.ClaveHash = hash;
                    resultado.Sesion.ClaveIteraciones = ClaveLocal.IteracionesPorDefecto;

                    // Se guarda por usuario: en un mismo POS pueden entrar varios cajeros.
                    _credenciales.Guardar(resultado.Sesion);
                    Sesion = resultado.Sesion;
                    DialogResult = true;
                }
            }
            catch (Exception)
            {
                // Sin conexión: se intenta entrar con el acceso guardado de ese usuario.
                SesionLocal guardada;
                string motivo;
                if (IntentarEntradaOffline(usuario, clave, out guardada, out motivo))
                {
                    Sesion = guardada;
                    DialogResult = true;
                    return;
                }

                Estado("No se pudo conectar con el servidor. " + motivo);
            }
            finally
            {
                BotonEntrar.IsEnabled = true;
            }
        }

        /// <summary>
        /// Entrada sin conexión para el usuario que escribió sus credenciales.
        /// Cada cajero tiene su propia sesión guardada (usuario, negocio y sucursal).
        /// </summary>
        private bool IntentarEntradaOffline(string usuario, string clave, out SesionLocal sesion, out string motivo)
        {
            sesion = null;
            motivo = "";

            var guardada = _credenciales.Buscar(usuario);
            if (guardada == null)
            {
                motivo = "Este usuario no tiene acceso guardado en este equipo.";
                return false;
            }
            if (!ReglaSesion.PuedeEntrarOffline(guardada, DateTimeOffset.Now))
            {
                motivo = "Pasaron más de 2 días sin conectar con este usuario.";
                return false;
            }
            if (!ClaveLocal.Verificar(clave, guardada.ClaveSal, guardada.ClaveHash, guardada.ClaveIteraciones))
            {
                motivo = "Contraseña incorrecta.";
                return false;
            }

            sesion = guardada;
            return true;
        }

        private void Ajustes_Click(object sender, RoutedEventArgs e)
        {
            var ajustes = new VentanaAjustes(_config) { Owner = this };
            if (ajustes.ShowDialog() == true)
            {
                Estado("Ajustes guardados.");
            }
        }

        private void Barra_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CampoClave_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                BotonEntrar_Click(sender, null);
            }
        }

        private void Estado(string texto)
        {
            TextoEstado.Text = texto;
        }
    }
}
