using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Iseller.Pos.Core.Actualizacion;
using Iseller.Pos.Core.Api;
using Iseller.Pos.Core.Auth;
using Iseller.Pos.Core.Caja;
using Iseller.Pos.Core.Carrito;
using Iseller.Pos.Core.Configuracion;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Imagenes;
using Iseller.Pos.Core.Impresion;
using Iseller.Pos.Core.Modelos;
using Iseller.Pos.Core.Precios;
using Newtonsoft.Json;

namespace Iseller.Pos.App
{
    public partial class MainWindow : Window
    {
        private const int FilasSinBusqueda = 200;
        private const int VentanaCodigoMs = 100;

        private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-VE");

        private readonly AppConfig _config;
        private readonly SesionLocal _sesion;
        private readonly AlmacenCredenciales _credenciales;
        private readonly CatalogoRepositorio _catalogo = new CatalogoRepositorio();
        private readonly CarritoRepositorio _carritoRepo = new CarritoRepositorio();
        private readonly VentasRepositorio _ventasRepo = new VentasRepositorio();
        private readonly CajaRepositorio _cajaRepo = new CajaRepositorio();
        private readonly ReservadosRepositorio _reservadosRepo = new ReservadosRepositorio();
        private readonly ImagenCache _imagenes;
        private readonly Carrito _carrito = new Carrito();
        private readonly ObservableCollection<ProductoVista> _resultados = new ObservableCollection<ProductoVista>();
        private readonly ObservableCollection<ItemCarritoVista> _lineas = new ObservableCollection<ItemCarritoVista>();
        private readonly ObservableCollection<VentaVista> _pendientes = new ObservableCollection<VentaVista>();
        private readonly List<VentaVista> _pendientesTodas = new List<VentaVista>();
        private readonly ObservableCollection<ReservadoVista> _reservados = new ObservableCollection<ReservadoVista>();
        private readonly ObservableCollection<VentaVista> _enviados = new ObservableCollection<VentaVista>();
        private readonly StringBuilder _codigo = new StringBuilder();

        private CalculadoraPrecios _calculadora;
        private Tasas _tasas;
        private bool _sincronizando;
        private bool _enviando;
        private bool _preguntandoCaja;
        private int? _filtroCajero;
        private EstadoCaja _estadoCaja;
        private DateTime _ultimaTeclaCodigo = DateTime.MinValue;
        private readonly DispatcherTimer _relojEnvio = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60),
        };

        public MainWindow(AppConfig config, SesionLocal sesion, AlmacenCredenciales credenciales)
        {
            InitializeComponent();

            _config = config;
            _sesion = sesion;
            _credenciales = credenciales;

            TextoSucursal.Text = "Sucursal: " + sesion.SucursalNombre;
            TablaResultados.ItemsSource = _resultados;
            TablaCarrito.ItemsSource = _lineas;
            TablaPendientes.ItemsSource = _pendientes;
            TablaReservados.ItemsSource = _reservados;
            TablaEnviados.ItemsSource = _enviados;
            _imagenes = new ImagenCache(config.BaseImagenes);

            BotonCredito.Visibility = TienePermiso(11) ? Visibility.Visible : Visibility.Collapsed;
            BotonDescuento.Visibility = TienePermiso(12) ? Visibility.Visible : Visibility.Collapsed;

            _relojEnvio.Tick += (s, e) =>
            {
                if (_ventasRepo.ContarPendientes() + _cajaRepo.ContarPendientes() > 0)
                {
                    _ = EnviarPendientesAsync();
                }
            };

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
            RefrescarReservados();
            RefrescarEnviados();
            AplicarEstadoCaja(true);
            CampoBusqueda.Focus();

            await EnviarPendientesAsync();
            await SincronizarAsync();
            _relojEnvio.Start();
            await BuscarActualizacionAsync(true);
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

            // Miniaturas: solo las primeras filas, y la caché evita repetir descargas.
            foreach (var vista in _resultados.Take(60))
            {
                _ = vista.CargarImagenAsync(_imagenes);
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
                TablaCarrito.Visibility = Visibility.Collapsed;
                CarritoVacio.Visibility = Visibility.Visible;
            }
            else
            {
                TextoTotales.Text = "TOTAL:   " + _carrito.TotalPeso.ToString("N0", Formato) + " Cop    " +
                                    _carrito.TotalBs.ToString("N2", Formato) + " Bs    $" +
                                    _carrito.TotalUsd.ToString("N2", Formato);
                TablaCarrito.Visibility = Visibility.Visible;
                CarritoVacio.Visibility = Visibility.Collapsed;
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
            if (CajaExigida && CajaReglas.BloqueaVenta(_estadoCaja))
            {
                Aviso(_estadoCaja == EstadoCaja.Cerrada
                    ? "La caja está cerrada. Reábrela para vender."
                    : "Abre la caja antes de vender.");
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

        // ── Carritos reservados ────────────────────────────────────────────────────

        private void Reservar_Click(object sender, RoutedEventArgs e)
        {
            if (_carrito.Vacio)
            {
                Aviso("El carrito está vacío.");
                return;
            }

            var ventana = new VentanaReserva { Owner = this };
            if (ventana.ShowDialog() != true)
            {
                return;
            }

            _reservadosRepo.Guardar(new Reservado
            {
                Id = Guid.NewGuid().ToString(),
                Cliente = ventana.Cliente,
                CreadaEn = DateTimeOffset.Now,
                ProductosJson = JsonConvert.SerializeObject(_carrito.Items),
            });

            _carrito.Vaciar();
            _carritoRepo.Vaciar();
            RefrescarCarrito();
            RefrescarReservados();
            Aviso("Carrito reservado.");
        }

        private void RefrescarReservados()
        {
            _reservados.Clear();
            var lista = _reservadosRepo.Listar();
            foreach (var reservado in lista)
            {
                _reservados.Add(new ReservadoVista { Reservado = reservado });
            }
            HeaderReservados.Text = lista.Count > 0
                ? "Reservados (" + lista.Count + ")"
                : "Reservados";
        }

        private void Retomar_Click(object sender, RoutedEventArgs e)
        {
            var vista = (sender as FrameworkElement)?.DataContext as ReservadoVista;
            if (vista == null)
            {
                return;
            }

            var reservado = _reservadosRepo.Obtener(vista.Reservado.Id);
            if (reservado == null)
            {
                RefrescarReservados();
                return;
            }

            _carrito.Cargar(reservado.Productos());
            _carritoRepo.Reemplazar(_carrito.Items);
            _reservadosRepo.Eliminar(reservado.Id);
            RefrescarCarrito();
            RefrescarReservados();
            TabControlPrincipal.SelectedIndex = 0;
            Aviso("Carrito retomado.");
        }

        private void EliminarReservado_Click(object sender, RoutedEventArgs e)
        {
            var vista = (sender as FrameworkElement)?.DataContext as ReservadoVista;
            if (vista == null)
            {
                return;
            }

            if (MessageBox.Show(this, "¿Eliminar el carrito reservado?", "Confirmar",
                                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            _reservadosRepo.Eliminar(vista.Reservado.Id);
            RefrescarReservados();
        }

        private void RefrescarEnviados()
        {
            _enviados.Clear();
            var lista = _ventasRepo.Enviadas(DateTimeOffset.Now.AddDays(-7), DateTimeOffset.Now.AddDays(1));
            foreach (var venta in lista)
            {
                _enviados.Add(new VentaVista { Venta = venta });
            }
            HeaderEnviados.Text = lista.Count > 0
                ? "Enviados (" + lista.Count + ")"
                : "Enviados";
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
            _ = EnviarPendientesAsync();

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

        // ── Caja ───────────────────────────────────────────────────────────────────

        private bool CajaExigida
        {
            get { return CajaReglas.Requiere(_sesion.Configuracion, _sesion.Nivel); }
        }

        private void AplicarEstadoCaja(bool preguntarApertura)
        {
            _estadoCaja = _cajaRepo.Estado(_sesion.UsuarioId, DateTime.Today);

            if (!CajaExigida)
            {
                AvisoCaja.Visibility = Visibility.Collapsed;
                BotonCerrarCaja.Visibility = Visibility.Collapsed;
                return;
            }

            BotonCerrarCaja.Visibility = _estadoCaja == EstadoCaja.Abierta
                ? Visibility.Visible
                : Visibility.Collapsed;
            BotonAbrirCaja.Visibility = _estadoCaja == EstadoCaja.SinApertura
                ? Visibility.Visible
                : Visibility.Collapsed;
            BotonReabrirCaja.Visibility = _estadoCaja == EstadoCaja.Cerrada
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (_estadoCaja == EstadoCaja.Abierta)
            {
                AvisoCaja.Visibility = Visibility.Collapsed;
                return;
            }

            AvisoCaja.Visibility = Visibility.Visible;
            TextoAvisoCaja.Text = _estadoCaja == EstadoCaja.Cerrada
                ? "Caja cerrada. Reabre la caja para volver a vender."
                : "La caja no está abierta. Registra la apertura para vender.";

            if (preguntarApertura && _estadoCaja == EstadoCaja.SinApertura)
            {
                AbrirCajaDialogo();
            }
        }

        private void AbrirCaja_Click(object sender, RoutedEventArgs e)
        {
            AbrirCajaDialogo();
        }

        private void AbrirCajaDialogo()
        {
            if (_preguntandoCaja)
            {
                return;
            }
            _preguntandoCaja = true;
            try
            {
                var ventana = new VentanaCaja(false) { Owner = this };
                if (ventana.ShowDialog() != true)
                {
                    return;
                }

                _cajaRepo.Guardar(new RegistroCaja
                {
                    Id = Guid.NewGuid().ToString(),
                    Tipo = "apertura",
                    CreadaEn = DateTimeOffset.Now,
                    Observaciones = ventana.Observaciones,
                    UsuarioId = _sesion.UsuarioId,
                    Montos = ventana.Montos,
                });

                AplicarEstadoCaja(false);
                Aviso("Caja abierta. El movimiento se enviará al sincronizar.");
                _ = EnviarPendientesAsync();
            }
            finally
            {
                _preguntandoCaja = false;
            }
        }

        private void CerrarCaja_Click(object sender, RoutedEventArgs e)
        {
            if (_estadoCaja != EstadoCaja.Abierta)
            {
                return;
            }

            var ventana = new VentanaCaja(true) { Owner = this };
            if (ventana.ShowDialog() != true)
            {
                return;
            }

            _cajaRepo.Guardar(new RegistroCaja
            {
                Id = Guid.NewGuid().ToString(),
                Tipo = "cierre",
                CreadaEn = DateTimeOffset.Now,
                Observaciones = ventana.Observaciones,
                UsuarioId = _sesion.UsuarioId,
                Montos = ventana.Montos,
            });

            AplicarEstadoCaja(false);
            Aviso("Caja cerrada. No se puede vender hasta reabrirla.");
            _ = EnviarPendientesAsync();
        }

        private async void ReabrirCaja_Click(object sender, RoutedEventArgs e)
        {
            var ultimo = _cajaRepo.UltimoDelDia(_sesion.UsuarioId, DateTime.Today);
            if (ultimo == null || !ultimo.EsCierre)
            {
                AplicarEstadoCaja(false);
                return;
            }

            try
            {
                // Primero sube lo pendiente (ventas y el propio cierre), luego reabre.
                await EnviarPendientesAsync();

                using (var api = new PosApiClient(_config.ApiBaseUrl, _config.ApiKey))
                {
                    var resultado = await api.ReabrirCajaAsync(_sesion.Token, ultimo.Id);
                    if (!resultado.Ok)
                    {
                        MessageBox.Show(this, resultado.Error ?? "No se pudo reabrir la caja.",
                            "Reabrir caja", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                _cajaRepo.Eliminar(ultimo.Id);
                AplicarEstadoCaja(false);
                Aviso("Caja reabierta.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Para reabrir la caja necesitas conexión: " + ex.Message,
                    "Reabrir caja", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RefrescarPendientes()
        {
            _pendientesTodas.Clear();
            foreach (var venta in _ventasRepo.Pendientes())
            {
                _pendientesTodas.Add(new VentaVista { Venta = venta });
            }

            var opciones = new List<CajeroOpcion>
            {
                new CajeroOpcion { Id = null, Nombre = "Todos los cajeros (" + _pendientesTodas.Count + ")" },
            };
            foreach (var grupo in _pendientesTodas
                         .GroupBy(v => new { v.Venta.UsuarioId, v.Venta.UsuarioNombre })
                         .OrderBy(g => g.Key.UsuarioNombre))
            {
                opciones.Add(new CajeroOpcion
                {
                    Id = grupo.Key.UsuarioId,
                    Nombre = grupo.Key.UsuarioNombre + " (" + grupo.Count() + ")",
                });
            }

            FiltroCajero.ItemsSource = opciones;
            FiltroCajero.SelectedIndex = 0; // dispara el filtro y llena la tabla

            HeaderPendientes.Text = _pendientesTodas.Count > 0
                ? "No guardados (" + _pendientesTodas.Count + ")"
                : "No guardados";

            TextoCatalogo.Text = (_ventasRepo.ContarPendientes() + _cajaRepo.ContarPendientes()) + " por enviar";
        }

        private void FiltroCajero_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var opcion = FiltroCajero.SelectedItem as CajeroOpcion;
            _filtroCajero = opcion != null ? opcion.Id : (int?)null;
            AplicarFiltroPendientes();
        }

        /// <summary>Al abrir "No guardados" se relee la cola, por si otro cajero dejó ventas.</summary>
        private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // El SelectionChanged de los combos de dentro burbujea hasta aquí: se filtra.
            if (!ReferenceEquals(e.OriginalSource, TabControlPrincipal))
            {
                return;
            }
            if (ReferenceEquals(TabControlPrincipal.SelectedItem, TabPendientes))
            {
                RefrescarPendientes();
            }
        }

        private void AplicarFiltroPendientes()
        {
            _pendientes.Clear();
            foreach (var vista in _pendientesTodas)
            {
                if (_filtroCajero == null || vista.Venta.UsuarioId == _filtroCajero)
                {
                    _pendientes.Add(vista);
                }
            }
        }

        private async void EnviarTodos_Click(object sender, RoutedEventArgs e)
        {
            await EnviarPendientesAsync(null);
        }

        private async void EnviarFiltrados_Click(object sender, RoutedEventArgs e)
        {
            await EnviarPendientesAsync(_filtroCajero);
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

        private async void Enviar_Click(object sender, RoutedEventArgs e)
        {
            await EnviarPendientesAsync();
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

        /// <summary>Sube las ventas pendientes y las marca según la respuesta por venta.</summary>
        private async Task EnviarPendientesAsync(int? usuarioId = null)
        {
            if (_enviando)
            {
                return;
            }

            var ventas = _ventasRepo.Pendientes()
                .Where(v => usuarioId == null || v.UsuarioId == usuarioId)
                .ToList();
            var caja = _cajaRepo.Pendientes()
                .Where(c => usuarioId == null || c.UsuarioId == usuarioId)
                .ToList();
            if (ventas.Count == 0 && caja.Count == 0)
            {
                return;
            }

            _enviando = true;
            PuntoConexion.Fill = (Brush)FindResource("WarningBrush");
            TextoEstadoConexion.Text = "Sincronizando " + (ventas.Count + caja.Count) + " registro(s)…";

            try
            {
                using (var api = new PosApiClient(_config.ApiBaseUrl, _config.ApiKey))
                {
                    int enviadas = 0;
                    int errores = 0;
                    int cajaOk = 0;
                    int cajaErr = 0;

                    // Cada cajero guarda su propio token: las ventas y las cajas se envían con el
                    // del cajero que las hizo (puede haber más de uno por día en el mismo POS).
                    foreach (var grupo in ventas.GroupBy(v => v.UsuarioId))
                    {
                        var sesionCajero = _credenciales.BuscarPorId(grupo.Key);
                        if (sesionCajero == null)
                        {
                            string nombreCajero = grupo.First().UsuarioNombre;
                            foreach (var venta in grupo)
                            {
                                _ventasRepo.RegistrarFallo(venta.IdPedido,
                                    "El cajero " + nombreCajero + " no tiene sesión guardada en este equipo; " +
                                    "debe entrar una vez con internet para poder enviar sus ventas.");
                                errores++;
                            }
                            continue;
                        }

                        var resultado = await api.EnviarVentasAsync(sesionCajero.Token, grupo.ToList());
                        if (!resultado.Ok)
                        {
                            foreach (var venta in grupo)
                            {
                                _ventasRepo.RegistrarFallo(venta.IdPedido, resultado.Error);
                                errores++;
                            }
                            continue;
                        }

                        foreach (var r in resultado.Resultados)
                        {
                            if (r.Ok)
                            {
                                _ventasRepo.MarcarEnviada(r.IdPedido, DateTimeOffset.Now);
                                enviadas++;
                            }
                            else
                            {
                                _ventasRepo.RegistrarFallo(r.IdPedido, r.Error);
                                errores++;
                            }
                        }
                    }

                    // La caja va después de las ventas: el cierre suma las órdenes del día.
                    foreach (var grupo in caja.GroupBy(c => c.UsuarioId))
                    {
                        var sesionCajero = _credenciales.BuscarPorId(grupo.Key);
                        if (sesionCajero == null)
                        {
                            cajaErr += grupo.Count();
                            continue;
                        }

                        foreach (var registro in grupo)
                        {
                            var resultadoCaja = await api.GuardarCajaAsync(sesionCajero.Token, registro);
                            if (resultadoCaja.Ok)
                            {
                                _cajaRepo.MarcarSincronizada(registro.Id, DateTimeOffset.Now);
                                cajaOk++;
                            }
                            else
                            {
                                cajaErr++;
                            }
                        }
                    }

                    Conexion(true, "En línea");
                    RefrescarPendientes();
                    RefrescarEnviados();

                    int totalOk = enviadas + cajaOk;
                    int totalError = errores + cajaErr;
                    if (totalOk > 0)
                    {
                        Aviso(totalOk + " registro(s) sincronizado(s)" +
                              (totalError > 0 ? ", " + totalError + " con error." : "."));
                        if (enviadas > 0)
                        {
                            await SincronizarAsync(); // el stock cambió: refresca el catálogo
                        }
                    }
                    else if (totalError > 0)
                    {
                        Aviso("No se pudieron enviar " + totalError + " registro(s).");
                    }
                }
            }
            catch (Exception ex)
            {
                Conexion(false, "Sin conexión · " + ex.Message);
            }
            finally
            {
                _enviando = false;
            }
        }

        private void Barra_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Minimizar_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void Actualizar_Click(object sender, RoutedEventArgs e)
        {
            await BuscarActualizacionAsync(false);
        }

        /// <summary>
        /// Consulta api/pos_version.php. Si hay versión nueva, descarga el ZIP, valida el
        /// SHA-256 y lo aplica al cerrar la app.
        /// </summary>
        private async Task BuscarActualizacionAsync(bool silenciosa)
        {
            var resultado = await Actualizador.BuscarAsync(_config.ApiBaseUrl, _config.ApiKey, App.VersionApp);

            if (!resultado.Ok)
            {
                if (!silenciosa)
                {
                    Aviso("No se pudo consultar la actualización: " + (resultado.Error ?? "sin conexión"));
                }
                return;
            }

            if (!resultado.Hay)
            {
                if (!silenciosa)
                {
                    MessageBox.Show(this, "Ya tienes la última versión (" + App.VersionApp + ").",
                        "Actualizar", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return;
            }

            var respuesta = MessageBox.Show(this,
                "Hay una versión nueva: " + resultado.Version + "\n\n" + (resultado.Notas ?? "") +
                "\n\n¿Descargar y aplicar ahora? La app se reiniciará.",
                "Actualización disponible", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (respuesta != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                string zip = Path.Combine(Path.Combine(BasePos.Directorio, "update"), "pos_update.zip");
                PuntoConexion.Fill = (Brush)FindResource("WarningBrush");
                TextoEstadoConexion.Text = "Descargando actualización…";

                await Actualizador.DescargarAsync(resultado.Url, zip);

                if (!Actualizador.VerificarSha256(zip, resultado.Sha256))
                {
                    File.Delete(zip);
                    MessageBox.Show(this,
                        "El archivo descargado no coincide con la firma esperada. No se aplicará.",
                        "Actualización", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Actualizador.Aplicar(zip, Process.GetCurrentProcess().MainModule.FileName);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo actualizar: " + ex.Message, "Actualización",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
