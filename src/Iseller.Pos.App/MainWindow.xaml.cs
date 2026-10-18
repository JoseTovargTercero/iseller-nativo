using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Iseller.Pos.Core.Api;
using Iseller.Pos.Core.Auth;
using Iseller.Pos.Core.Configuracion;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Modelos;
using Iseller.Pos.Core.Precios;

namespace Iseller.Pos.App
{
    public partial class MainWindow : Window
    {
        private const int FilasSinBusqueda = 200;

        private readonly AppConfig _config;
        private readonly SesionLocal _sesion;
        private readonly CatalogoRepositorio _repo;
        private readonly ObservableCollection<ProductoVista> _filas = new ObservableCollection<ProductoVista>();

        private CalculadoraPrecios _calculadora;
        private bool _sincronizando;

        public MainWindow(AppConfig config, SesionLocal sesion)
        {
            InitializeComponent();

            _config = config;
            _sesion = sesion;
            _repo = new CatalogoRepositorio();

            TextoSucursal.Text = "Sucursal: " + sesion.SucursalNombre;
            TextoEstado.Text = sesion.Nombre + " · " + (sesion.Nivel == 1 ? "administrador" : "cajero");

            TablaProductos.ItemsSource = _filas;
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CargarDesdeDisco();
            await SincronizarAsync();
        }

        // ── Catálogo local ─────────────────────────────────────────────────────────

        private void CargarDesdeDisco()
        {
            var tasas = _repo.ObtenerTasas();
            _calculadora = CrearCalculadora(tasas);
            ActualizarTasas(tasas);

            int total = _repo.ContarProductos();
            if (total == 0)
            {
                _filas.Clear();
                MensajeVacio.Visibility = Visibility.Visible;
                TextoCatalogo.Text = "Sin catálogo descargado";
                return;
            }

            MensajeVacio.Visibility = Visibility.Collapsed;
            Mostrar(_repo.Productos().Take(FilasSinBusqueda));
            TextoCatalogo.Text = total + " productos · descargado " + FechaCatalogo();
        }

        private CalculadoraPrecios CrearCalculadora(Tasas tasas)
        {
            if (tasas == null)
            {
                return null;
            }
            return new CalculadoraPrecios(new ParametrosPrecios
            {
                PesoDolar = tasas.PesoDolar,
                PesoBolivar = tasas.PesoBolivar,
                DolarBolivar = tasas.DolarBolivar,
                BolivarPeso = tasas.BolivarPeso,
                Bcv = tasas.Bcv,
                PrecioDolarVisible = tasas.PrecioDolarVisible,
                PrecioPesoVisible = tasas.PrecioPesoVisible,
            });
        }

        private void Mostrar(IEnumerable<Producto> productos)
        {
            _filas.Clear();
            if (_calculadora == null)
            {
                return;
            }
            foreach (var p in productos)
            {
                _filas.Add(ProductoVista.Desde(p, _calculadora));
            }
        }

        private void Buscar(string termino)
        {
            if (_calculadora == null)
            {
                return;
            }

            string limpio = (termino ?? "").Trim();
            if (limpio.Length < 2)
            {
                Mostrar(_repo.Productos().Take(FilasSinBusqueda));
                return;
            }

            Mostrar(_repo.Buscar(limpio, 100));
        }

        private void ActualizarTasas(Tasas tasas)
        {
            if (tasas == null)
            {
                AvisoTasas.Visibility = Visibility.Visible;
                TextoAvisoTasas.Text = "No hay tasas de cambio descargadas. Conecta para obtenerlas.";
                return;
            }

            TimeSpan? antiguedad = ReglaSesion.AntiguedadTasa(tasas, DateTimeOffset.Now);
            string texto = antiguedad.HasValue ? AntiguedadLegible(antiguedad.Value) : "desconocida";

            if (ReglaSesion.TasaVencida(tasas, DateTimeOffset.Now))
            {
                AvisoTasas.Visibility = Visibility.Visible;
                TextoAvisoTasas.Text = "La tasa tiene " + texto +
                    " sin actualizar. Límite para vender: 48 h. Conecta para seguir vendiendo.";
            }
            else
            {
                AvisoTasas.Visibility = Visibility.Collapsed;
            }

            TextoEstado.Text = _sesion.Nombre + " · " + (_sesion.Nivel == 1 ? "administrador" : "cajero") +
                               " · tasa " + texto;
        }

        // ── Sincronización ─────────────────────────────────────────────────────────

        private async Task SincronizarAsync()
        {
            if (_sincronizando)
            {
                return;
            }
            _sincronizando = true;
            PuntoConexion.Fill = (Brush)FindResource("WarningBrush");
            TextoEstadoConexion.Text = "Sincronizando…";

            try
            {
                using (var api = new PosApiClient(_config.ApiBaseUrl, _config.ApiKey))
                {
                    var hora = await api.HeartbeatAsync(_sesion.Token);
                    if (!hora.HasValue)
                    {
                        Conexion(false, "Sin conexión (o sesión vencida)");
                        return;
                    }

                    Conexion(true, "En línea");

                    var catalogo = await api.DescargarCatalogoAsync(_sesion.Token, _repo.Version());
                    if (!catalogo.Ok)
                    {
                        TextoEstadoConexion.Text = "En línea · " + catalogo.Error;
                        return;
                    }

                    if (!catalogo.SinCambios)
                    {
                        _repo.GuardarCatalogo(
                            catalogo.Productos,
                            catalogo.Clientes,
                            catalogo.Tasas,
                            catalogo.Configuracion,
                            _sesion.SucursalId,
                            catalogo.Version,
                            catalogo.ServidorEn);
                    }

                    CargarDesdeDisco();
                }
            }
            catch (Exception ex)
            {
                Conexion(false, "Sin conexión · " + ex.Message);
            }
            finally
            {
                _sincronizando = false;
            }
        }

        private void Conexion(bool enLinea, string texto)
        {
            PuntoConexion.Fill = (Brush)FindResource(enLinea ? "MintBrush" : "DangerBrush");
            TextoEstadoConexion.Text = texto;
        }

        // ── Eventos ────────────────────────────────────────────────────────────────

        private void CampoBusqueda_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Buscar(CampoBusqueda.Text);
        }

        private void CampoBusqueda_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }
            e.Handled = true;

            // Enter = lectura de código de barras: si coincide exacto, se muestra solo ese producto.
            var porCodigo = _repo.BuscarPorCodigo(CampoBusqueda.Text.Trim());
            if (porCodigo != null)
            {
                Mostrar(new[] { porCodigo });
                return;
            }
            Buscar(CampoBusqueda.Text);
        }

        private async void Sincronizar_Click(object sender, RoutedEventArgs e)
        {
            await SincronizarAsync();
        }

        private async void Verificar_Click(object sender, RoutedEventArgs e)
        {
            PuntoConexion.Fill = (Brush)FindResource("WarningBrush");
            TextoEstadoConexion.Text = "Comprobando…";
            try
            {
                using (var api = new PosApiClient(_config.ApiBaseUrl, _config.ApiKey))
                {
                    var hora = await api.HeartbeatAsync(_sesion.Token);
                    Conexion(hora.HasValue, hora.HasValue ? "En línea" : "Sin conexión (o sesión vencida)");
                }
            }
            catch (Exception)
            {
                Conexion(false, "Sin conexión");
            }
        }

        // ── Detalles ───────────────────────────────────────────────────────────────

        private string FechaCatalogo()
        {
            var fecha = _repo.ServidorEn();
            return fecha.HasValue ? fecha.Value.ToLocalTime().ToString("dd/MM HH:mm") : "—";
        }

        private static string AntiguedadLegible(TimeSpan valor)
        {
            if (valor.TotalMinutes < 60)
            {
                return valor.TotalMinutes.ToString("0", CultureInfo.InvariantCulture) + " min";
            }
            if (valor.TotalHours < 48)
            {
                return valor.TotalHours.ToString("0", CultureInfo.InvariantCulture) + " h";
            }
            return valor.TotalDays.ToString("0", CultureInfo.InvariantCulture) + " días";
        }
    }
}
