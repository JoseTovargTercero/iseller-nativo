using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Iseller.Pos.Core.Imagenes;
using Iseller.Pos.Core.Modelos;
using Iseller.Pos.Core.Precios;

namespace Iseller.Pos.App
{
    /// <summary>
    /// Modal que aparece al escanear un producto: foto, nombre, precios, cantidad y una
    /// barra de tiempo que lo agrega solo al carrito. Cualquier tecla detiene la cuenta.
    /// </summary>
    public partial class VentanaEscaner : Window
    {
        private readonly Producto _producto;
        private readonly Action<Producto, double> _alAgregar;
        private readonly ProductoVista _vista;
        private readonly int _segundos;
        private readonly DispatcherTimer _reloj;

        private DateTime _inicio;
        private DateTime _abiertoEn;
        private bool _agregado;
        private bool _cuentaActiva;

        public VentanaEscaner(Producto producto, ImagenCache imagenes, CalculadoraPrecios calculadora,
                              int segundos, Action<Producto, double> alAgregar)
        {
            InitializeComponent();

            _producto = producto;
            _alAgregar = alAgregar;
            _segundos = segundos < 0 ? 0 : (segundos > 30 ? 30 : segundos);
            _vista = ProductoVista.Desde(producto, calculadora);
            DataContext = _vista;

            _reloj = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _reloj.Tick += (s, e) => Avanzar();

            Loaded += (s, e) =>
            {
                BotonAgregar.Focus();
                if (imagenes != null && !string.IsNullOrWhiteSpace(producto.Foto))
                {
                    var tarea = _vista.CargarImagenAsync(imagenes);
                }
                IniciarCuenta();
            };
        }

        private void IniciarCuenta()
        {
            if (_segundos <= 0)
            {
                PistaBarra.Visibility = Visibility.Collapsed;
                TextoAyuda.Text = "Enter o el botón para agregar.";
                return;
            }

            _cuentaActiva = true;
            _inicio = DateTime.Now;
            _abiertoEn = DateTime.Now;
            BarraTiempo.Width = 0;
            PistaBarra.Visibility = Visibility.Visible;
            _reloj.Start();
            TextoAyuda.Text = "Se agrega solo en " + _segundos + " s · pulsa una tecla o mueve el mouse para detenerlo.";
        }

        private void Avanzar()
        {
            double total = _segundos * 1000.0;
            double transcurrido = (DateTime.Now - _inicio).TotalMilliseconds;
            double restante = Math.Max(0, total - transcurrido);

            BarraTiempo.Width = Math.Max(0, PistaBarra.ActualWidth * (restante / total));

            if (restante <= 0)
            {
                DetenerCuenta();
                Agregar();
            }
        }

        private void DetenerCuenta()
        {
            _reloj.Stop();
            _cuentaActiva = false;
            PistaBarra.Visibility = Visibility.Collapsed;
            TextoAyuda.Text = "Enter o el botón para agregar.";
        }

        private void Ventana_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    Agregar();
                    return;
                case Key.Up:
                case Key.Add:
                case Key.OemPlus:
                    e.Handled = true;
                    DetenerCuenta();
                    Cambiar(1);
                    return;
                case Key.Down:
                case Key.Subtract:
                case Key.OemMinus:
                    e.Handled = true;
                    DetenerCuenta();
                    Cambiar(-1);
                    return;
                case Key.Escape:
                    e.Handled = true;
                    Close();
                    return;
            }

            // Cualquier otra tecla detiene la cuenta automática.
            if (_cuentaActiva)
            {
                DetenerCuenta();
            }
        }

        private void Cambiar(int delta)
        {
            double actual = Core.Utilidades.Numero.ADouble(_vista.CantidadTexto);
            double nueva = Math.Max(1, Math.Round(actual, 2) + delta);
            _vista.CantidadTexto = nueva.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void Cantidad_Menos(object sender, RoutedEventArgs e)
        {
            Cambiar(-1);
        }

        private void Cantidad_Mas(object sender, RoutedEventArgs e)
        {
            Cambiar(1);
        }

        private void Agregar_Click(object sender, RoutedEventArgs e)
        {
            Agregar();
        }

        private void Agregar()
        {
            if (_agregado)
            {
                return;
            }
            _agregado = true;
            _reloj.Stop();

            double cantidad = Core.Utilidades.Numero.ADouble(_vista.CantidadTexto);
            if (cantidad <= 0)
            {
                cantidad = 1;
            }
            if (_alAgregar != null)
            {
                _alAgregar(_producto, cantidad);
            }
            Close();
        }

        private void Barra_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>Clic o movimiento del mouse también abortan la cuenta (salvo el primer instante).</summary>
        private void AbortarPorRaton()
        {
            if ((DateTime.Now - _abiertoEn).TotalMilliseconds < 300)
            {
                return;
            }
            if (_cuentaActiva)
            {
                DetenerCuenta();
            }
        }

        private void Ventana_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            AbortarPorRaton();
        }

        private void Ventana_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            AbortarPorRaton();
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
