using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.App
{
    /// <summary>
    /// Últimas ventas del cajero con opción de deshacerlas. La venta se deshace desde aquí
    /// pero el trabajo lo hace la ventana principal: ella sabe si ya se envió (hay que anularla
    /// en el servidor para devolver el stock) o si sigue en la cola local.
    /// </summary>
    public partial class VentanaUltimasVentas : Window
    {
        private readonly Func<Venta, System.Threading.Tasks.Task<string>> _deshacer;
        private readonly ObservableCollection<VentaVista> _ventas = new ObservableCollection<VentaVista>();

        public VentanaUltimasVentas(IEnumerable<Venta> ventas, Func<Venta, System.Threading.Tasks.Task<string>> deshacer)
        {
            InitializeComponent();

            _deshacer = deshacer;
            if (ventas != null)
            {
                foreach (var venta in ventas)
                {
                    _ventas.Add(new VentaVista { Venta = venta });
                }
            }

            TablaVentas.ItemsSource = _ventas;
            ActualizarEstado();
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

        private void ActualizarEstado()
        {
            if (_ventas.Count == 0)
            {
                MensajeVacio.Visibility = Visibility.Visible;
                TextoEstado.Text = "";
                return;
            }

            MensajeVacio.Visibility = Visibility.Collapsed;
            TextoEstado.Text = _ventas.Count + " venta(s). Deshacer devuelve los productos al stock.";
        }

        private async void Deshacer_Click(object sender, RoutedEventArgs e)
        {
            var vista = (sender as FrameworkElement)?.DataContext as VentaVista;
            if (vista == null || _deshacer == null)
            {
                return;
            }

            var venta = vista.Venta;
            string pregunta = venta.Estado == EstadoVenta.Enviada
                ? "Esta venta ya está en el servidor: se anulará y sus productos volverán al stock. ¿Continuar?"
                : "Esta venta todavía no se ha enviado: se quitará de la caja. ¿Continuar?";
            if (MessageBox.Show(this, pregunta, "Deshacer venta", MessageBoxButton.YesNo,
                                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            var boton = sender as Button;
            if (boton != null)
            {
                boton.IsEnabled = false;
            }
            TextoEstado.Text = "Deshaciendo la venta…";

            string error;
            try
            {
                error = await _deshacer(venta);
            }
            catch (Exception ex)
            {
                error = "No se pudo deshacer la venta: " + ex.Message;
            }

            if (error == null)
            {
                _ventas.Remove(vista);
                ActualizarEstado();
                TextoEstado.Text = "Venta deshecha. Los productos volvieron al stock.";
                return;
            }

            if (boton != null)
            {
                boton.IsEnabled = true;
            }
            TextoEstado.Text = error;
        }
    }
}
