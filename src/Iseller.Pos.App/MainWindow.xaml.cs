using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Iseller.Pos.Core.Api;
using Iseller.Pos.Core.Auth;
using Iseller.Pos.Core.Carrito;
using Iseller.Pos.Core.Configuracion;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Impresion;
using Iseller.Pos.Core.Modelos;
using Iseller.Pos.Core.Precios;

namespace Iseller.Pos.App
{
    public partial class MainWindow : Window
    {
        private const int FilasSinBusqueda = 200;
        private const int VentanaCodigoMs = 100;

        private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-VE");

        private readonly AppConfig _config;
        private readonly SesionLocal _sesion;
        private readonly CatalogoRepositorio _catalogo = new CatalogoRepositorio();
        private readonly CarritoRepositorio _carritoRepo = new CarritoRepositorio();
        private readonly VentasRepositorio _ventasRepo = new VentasRepositorio();
        private readonly Carrito _carrito = new Carrito();
        private readonly ObservableCollection<ProductoVista> _resultados = new ObservableCollection<ProductoVista>();
        private readonly ObservableCollection<ItemCarritoVista> _lineas = new ObservableCollection<ItemCarritoVista>();
        private readonly ObservableCollection<VentaVista> _pendientes = new ObservableCollection<VentaVista>();
        private readonly StringBuilder _codigo = new StringBuilder();

        private CalculadoraPrecios _calculadora;
        private Tasas _tasas;
        private bool _sincronizando;
        private DateTime _ultimaTeclaCodigo = DateTime.MinValue;

        public MainWindow(AppConfig config, SesionLocal sesion)
        {
            InitializeComponent();

            _config = config;
            _sesion = sesion;

            TextoSucursal.Text = "Sucursal: " + sesion.SucursalNombre;
            TablaResultados.ItemsSource = _resultados;
            TablaCarrito.ItemsSource = _lineas;
            TablaPendientes.ItemsSource = _pendientes;

            BotonCredito.Visibility = TienePermiso(11) ? Visibility.Visible : Visibility.Collapsed;
            BotonDescuento.Visibility = TienePermiso(12) ? Visibility.Visible : Visibility.Collapsed;

            Loaded += MainWindow_Loaded;
        }

        private bool TienePermiso(int idMenu)
        {
            return _sesion.Nivel == 1 || _sesion.Permisos.Contains(idMenu);
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CargarDesdeDisco();
            CargarCarrito();
            RefrescarPendientes();
            CampoBusqueda.Focus();

            await SincronizarAsync();
        }

        // ── Catálogo ───────────────────────────────────────────────────────────────

        private void CargarDesdeDisco()
        {
            _tasas = _catalogo.ObtenerTasas();
            _calculadora = CrearCalculadora(_tasas);
            ActualizarTasas(_tasas);

            int total = _catalogo.ContarProductos();
            if (total == 0)
            {
                _resultados.Clear();
                TextoCatalogo.Text = "Sin catálogo descargado";
                return;
            }

            Mostrar(_catalogo.Productos().Take(FilasSinBusqueda));
            TextoCatalogo.Text = _ventasRepo.ContarPendientes() + " por enviar · " + total + " productos";
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
            _resultados.Clear();
            if (_calculadora == null)
            {
                return;
            }
            foreach (var producto in productos)
            {
                _resultados.Add(ProductoVista.Desde(producto, _calculadora));
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
                Mostrar(_catalogo.Productos().Take(FilasSinBusqueda));
                return;
            }
            Mostrar(_catalogo.Buscar(limpio, 100));
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

        // ── Carrito ────────────────────────────────────────────────────────────────

        private void CargarCarrito()
        {
            _carrito.Cargar(_carritoRepo.Cargar());
            RefrescarCarrito();
        }

        private void PersistirCarrito()
        {
            _carritoRepo.Reemplazar(_carrito.Items);
        }

        private void RefrescarCarrito()
        {
            _lineas.Clear();
            foreach (var item in _carrito.Items.OrderBy(i => i.Name))
            {
                _lineas.Add(new ItemCarritoVista { Item = item });
            }

            if (_carrito.Vacio)
            {
                TextoTotales.Text = "Carrito vacío";
            }
            else
            {
                TextoTotales.Text = "TOTAL:   " + _carrito.TotalPeso.ToString("N0", Formato) + " Cop    " +
                                    _carrito.TotalBs.ToString("N2", Formato) + " Bs    $" +
                                    _carrito.TotalUsd.ToString("N2", Formato);
            }

            TextoCatalogo.Text = _ventasRepo.ContarPendientes() + " por enviar";
        }

        private bool PuedeVender()
        {
            if (_carrito.Vacio)
            {
                Aviso("El carrito está vacío.");
                return false;
            }
            if (_calculadora == null || ReglaSesion.TasaVencida(_tasas, DateTimeOffset.Now))
            {
                Aviso("No se puede vender: la tasa está vencida o no hay tasas. Conecta y sincroniza.");
                return false;
            }
            return true;
        }

        private void Agregar(Producto producto, double cantidad)
        {
            if (producto == null || _calculadora == null)
            {
                Aviso("Sincroniza el catálogo antes de vender.");
                return;
            }

            _carrito.Agregar(producto, _calculadora, cantidad);
            PersistirCarrito();
            RefrescarCarrito();

            CampoBusqueda.Text = "";
            CampoBusqueda.Focus();
        }

        private void Agregar_Click(object sender, RoutedEventArgs e)
        {
            var vista = (sender as FrameworkElement)?.DataContext as ProductoVista;
            if (vista != null)
            {
                Agregar(vista.Modelo, 1);
            }
        }

        private void TablaResultados_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var vista = TablaResultados.SelectedItem as ProductoVista;
            if (vista != null)
            {
                Agregar(vista.Modelo, 1);
            }
        }

        private void Sumar_Click(object sender, RoutedEventArgs e)
        {
            Ajustar(sender, 1);
        }

        private void Restar_Click(object sender, RoutedEventArgs e)
        {
            Ajustar(sender, -1);
        }

        private void Ajustar(object sender, double delta)
        {
            var vista = (sender as FrameworkElement)?.DataContext as ItemCarritoVista;
            if (vista == null)
            {
                return;
            }
            _carrito.Ajustar(vista.Item.Id, delta);
            PersistirCarrito();
            RefrescarCarrito();
        }

        private void Quitar_Click(object sender, RoutedEventArgs e)
        {
            var vista = (sender as FrameworkElement)?.DataContext as ItemCarritoVista;
            if (vista == null)
            {
                return;
            }
            _carrito.Quitar(vista.Item.Id);
            PersistirCarrito();
            RefrescarCarrito();
        }

        private void Destruir_Click(object sender, RoutedEventArgs e)
        {
            if (_carrito.Vacio)
            {
                return;
            }
            if (MessageBox.Show(this, "¿Vaciar el carrito?", "Confirmar",
                                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            _carrito.Vaciar();
            _carritoRepo.Vaciar();
            RefrescarCarrito();
        }

        // ── Venta ──────────────────────────────────────────────────────────────────

        private void Vender_Click(object sender, RoutedEventArgs e)
        {
            Vender();
        }

        private void Vender()
        {
            if (!PuedeVender())
            {
                return;
            }

            var dialogo = new VentanaPago(false, _sesion.Configuracion.RegistroClientes, _carrito, BuscarCliente)
            {
                Owner = this,
            };
            if (dialogo.ShowDialog() == true)
            {
                ProcesarVenta(dialogo.MetodoPago, Despacho.Venta, dialogo.Cliente);
            }
        }

        private void Credito_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeVender())
            {
                return;
            }

            var dialogo = new VentanaPago(true, 1, _carrito, BuscarCliente) { Owner = this };
            if (dialogo.ShowDialog() == true)
            {
                ProcesarVenta("0", Despacho.Credito, dialogo.Cliente);
            }
        }

        private void Descuento_Click(object sender, RoutedEventArgs e)
        {
            if (!PuedeVender())
            {
                return;
            }
            if (MessageBox.Show(this, "Se descontarán productos del almacén. ¿Continuar?", "Confirmar",
                                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }
            ProcesarVenta("0", Despacho.Descuento, new Cliente());
        }

        private void ProcesarVenta(string metodoPago, int despacho, Cliente cliente)
        {
            Venta venta;
            try
            {
                venta = VentaFabrica.Crear(_carrito, metodoPago, despacho, cliente, _sesion, _tasas, DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "No se pudo armar la venta", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var validacion = ValidadorVenta.Validar(venta);
            if (!validacion.Valido)
            {
                MessageBox.Show(this, validacion.Error, "Venta inválida", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                _ventasRepo.Guardar(venta);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo guardar la venta: " + ex.Message, "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _carrito.Vaciar();
            _carritoRepo.Vaciar();
            RefrescarCarrito();
            RefrescarPendientes();

            if (_sesion.Configuracion.Tickets == 1)
            {
                Imprimir(venta);
            }

            Aviso("Venta guardada. Queda como pendiente de enviar.");
        }

        private void Imprimir(Venta venta)
        {
            try
            {
                var opciones = _config.ComoOpcionesImpresion();
                if (!opciones.ImprimirTickets)
                {
                    return;
                }
                byte[] datos = TicketEscPos.Generar(venta, _sesion.SucursalNombre, opciones);
                ImpresoraRaw.Enviar(opciones.Impresora, datos);
            }
            catch (Exception ex)
            {
                Aviso("La venta se guardó, pero no se pudo imprimir: " + ex.Message);
            }
        }

        private void Cambio_Click(object sender, RoutedEventArgs e)
        {
            if (_carrito.Vacio)
            {
                Aviso("El carrito está vacío.");
                return;
            }

            var ventana = new VentanaCambio(_carrito.TotalPeso, _carrito.TotalUsd, _carrito.TotalBs)
            {
                Owner = this,
            };
            ventana.ShowDialog();
        }

        private Cliente BuscarCliente(string cedula)
        {
            return _catalogo.ObtenerCliente(cedula);
        }

        private void RefrescarPendientes()
        {
            _pendientes.Clear();
            var pendientes = _ventasRepo.Pendientes();
            foreach (var venta in pendientes)
            {
                _pendientes.Add(new VentaVista { Venta = venta });
            }
            HeaderPendientes.Text = pendientes.Count > 0
                ? "No guardados (" + pendientes.Count + ")"
                : "No guardados";

            TextoCatalogo.Text = pendientes.Count + " por enviar";
        }

        // ── Atajos y lector de barras ──────────────────────────────────────────────

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CampoBusqueda.Text = "";
                CampoBusqueda.Focus();
                e.Handled = true;
                return;
            }

            bool enCampo = Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is PasswordBox;

            if (e.Key == Key.Enter && !enCampo)
            {
                e.Handled = true;
                CerrarCodigo();
                return;
            }

            if (enCampo || e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl || e.Key == Key.LeftAlt ||
                e.Key == Key.RightAlt)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.B:
                    e.Handled = true;
                    CampoBusqueda.Focus();
                    return;
                case Key.V:
                    e.Handled = true;
                    Vender();
                    return;
                case Key.C:
                    e.Handled = true;
                    Cambio_Click(sender, null);
                    return;
                case Key.Add:
                case Key.OemPlus:
                    e.Handled = true;
                    AjustarSeleccionado(1);
                    return;
                case Key.Subtract:
                case Key.OemMinus:
                    e.Handled = true;
                    AjustarSeleccionado(-1);
                    return;
            }

            // Números: agregan el resultado N (1-9) de la búsqueda en curso.
            int indice = NumeroDe(e.Key);
            if (indice > 0)
            {
                e.Handled = true;
                if (indice <= _resultados.Count)
                {
                    Agregar(_resultados[indice - 1].Modelo, 1);
                }
                return;
            }

            // Lector de barras: acumula dígitos si llegan en ráfaga.
            AcumularCodigo(e);
        }

        private void AjustarSeleccionado(double delta)
        {
            var vista = TablaCarrito.SelectedItem as ItemCarritoVista;
            if (vista == null)
            {
                return;
            }
            _carrito.Ajustar(vista.Item.Id, delta);
            PersistirCarrito();
            RefrescarCarrito();
        }

        private static int NumeroDe(Key tecla)
        {
            if (tecla >= Key.D1 && tecla <= Key.D9)
            {
                return tecla - Key.D1 + 1;
            }
            if (tecla >= Key.NumPad1 && tecla <= Key.NumPad9)
            {
                return tecla - Key.NumPad1 + 1;
            }
            return 0;
        }

        private void AcumularCodigo(KeyEventArgs e)
        {
            char letra = TeclaADigito(e);
            if (letra == '\0')
            {
                return;
            }

            var ahora = DateTime.Now;
            if ((ahora - _ultimaTeclaCodigo).TotalMilliseconds > VentanaCodigoMs)
            {
                _codigo.Clear();
            }
            _ultimaTeclaCodigo = ahora;
            _codigo.Append(letra);
            e.Handled = true;
        }

        private static char TeclaADigito(KeyEventArgs e)
        {
            if (e.Key >= Key.D0 && e.Key <= Key.D9 && Keyboard.Modifiers == ModifierKeys.None)
            {
                return (char)('0' + (e.Key - Key.D0));
            }
            if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                return (char)('0' + (e.Key - Key.NumPad0));
            }
            return '\0';
        }

        /// <summary>Al terminar una lectura, busca el código exacto y lo agrega.</summary>
        private void CerrarCodigo()
        {
            if (_codigo.Length == 0)
            {
                return;
            }

            string codigo = _codigo.ToString();
            _codigo.Clear();
            if (codigo.Length < 6)
            {
                return;
            }

            var producto = _catalogo.BuscarPorCodigo(codigo);
            if (producto == null)
            {
                Aviso("El producto con código " + codigo + " no está en el catálogo.");
                return;
            }
            Agregar(producto, 1);
        }

        // ── Eventos de UI ──────────────────────────────────────────────────────────

        private void CampoBusqueda_TextChanged(object sender, TextChangedEventArgs e)
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

            string termino = CampoBusqueda.Text.Trim();

            // Enter = lectura de código de barras: si coincide exacto, entra al carrito.
            var porCodigo = _catalogo.BuscarPorCodigo(termino);
            if (porCodigo != null)
            {
                Agregar(porCodigo, 1);
                return;
            }

            if (_resultados.Count == 1)
            {
                Agregar(_resultados[0].Modelo, 1);
            }
        }

        private void Ajustes_Click(object sender, RoutedEventArgs e)
        {
            var ajustes = new VentanaAjustes(_config) { Owner = this };
            if (ajustes.ShowDialog() == true)
            {
                Aviso("Ajustes guardados.");
            }
        }

        // ── Sincronización y auxiliares ────────────────────────────────────────────

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

                    var catalogo = await api.DescargarCatalogoAsync(_sesion.Token, _catalogo.Version());
                    if (!catalogo.Ok)
                    {
                        TextoEstadoConexion.Text = "En línea · " + catalogo.Error;
                        return;
                    }

                    if (!catalogo.SinCambios)
                    {
                        _catalogo.GuardarCatalogo(
                            catalogo.Productos,
                            catalogo.Clientes,
                            catalogo.Tasas,
                            catalogo.Configuracion,
                            _sesion.SucursalId,
                            catalogo.Version,
                            catalogo.ServidorEn);
                    }

                    CargarDesdeDisco();
                    Buscar(CampoBusqueda.Text);
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

        private void Aviso(string mensaje)
        {
            TextoEstadoConexion.Text = mensaje;
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
