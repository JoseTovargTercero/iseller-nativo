using System;
using System.Collections.Generic;
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
        private readonly SesionLocal _sesionGuardada;
        private int? _sucursalElegida;

        public SesionLocal Sesion { get; private set; }

        public LoginWindow(AppConfig config, AlmacenCredenciales credenciales, SesionLocal sesionGuardada)
        {
            InitializeComponent();

            _config = config;
            _credenciales = credenciales;
            _sesionGuardada = sesionGuardada;

            if (sesionGuardada != null)
            {
                CampoUsuario.Text = sesionGuardada.Usuario;
                if (ReglaSesion.PuedeEntrarOffline(sesionGuardada, DateTimeOffset.Now))
                {
                    BotonOffline.Visibility = Visibility.Visible;
                    Estado("Sesión guardada de " + sesionGuardada.Nombre +
                           ". Puedes entrar sin conexión hasta el " +
                           sesionGuardada.UltimoLoginValidoEn.Add(ReglaSesion.LimiteOffline)
                               .ToLocalTime().ToString("dd/MM HH:mm") + ".");
                }
                else
                {
                    Estado("La sesión guardada venció (más de 2 días sin conectar). Hay que entrar con internet.");
                }
            }
            else
            {
                Estado("Primer acceso: se necesita conexión a internet.");
            }

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

                    _credenciales.Guardar(resultado.Sesion);
                    Sesion = resultado.Sesion;
                    DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                Estado("No se pudo conectar con el servidor. " + ex.Message +
                       "  (Con una sesión guardada puedes usar \"Entrar sin conexión\".)");
            }
            finally
            {
                BotonEntrar.IsEnabled = true;
            }
        }

        private void BotonOffline_Click(object sender, RoutedEventArgs e)
        {
            if (_sesionGuardada == null)
            {
                return;
            }

            if (!string.Equals(CampoUsuario.Text.Trim(), _sesionGuardada.Usuario, StringComparison.OrdinalIgnoreCase))
            {
                Estado("La sesión guardada es de otro usuario. Entra con internet para cambiarla.");
                return;
            }

            if (!ReglaSesion.PuedeEntrarOffline(_sesionGuardada, DateTimeOffset.Now))
            {
                Estado("Pasaron más de 2 días sin conectar. Necesitas internet para volver a entrar.");
                return;
            }

            if (!ClaveLocal.Verificar(CampoClave.Password, _sesionGuardada.ClaveSal,
                                      _sesionGuardada.ClaveHash, _sesionGuardada.ClaveIteraciones))
            {
                Estado("Contraseña incorrecta.");
                return;
            }

            Sesion = _sesionGuardada;
            DialogResult = true;
        }

        private void Ajustes_Click(object sender, RoutedEventArgs e)
        {
            var ajustes = new VentanaAjustes(_config) { Owner = this };
            if (ajustes.ShowDialog() == true)
            {
                Estado("Ajustes guardados.");
            }
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
