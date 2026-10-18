using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Iseller.Pos.Core.Carrito;
using Iseller.Pos.Core.Modelos;

namespace Iseller.Pos.App
{
    public partial class VentanaPago : Window
    {
        private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-VE");

        private readonly bool _esCredito;
        private readonly bool _pideCliente;
        private readonly Func<string, Cliente> _buscarCliente;

        public string MetodoPago { get; private set; }
        public Cliente Cliente { get; private set; }

        public VentanaPago(bool esCredito, int registroClientes, Carrito carrito, Func<string, Cliente> buscarCliente)
        {
            InitializeComponent();

            _esCredito = esCredito;
            _pideCliente = esCredito || registroClientes == 1;
            _buscarCliente = buscarCliente;

            TextoTipo.Text = esCredito ? "Se registrará como CRÉDITO del cliente" : "Venta de contado";
            TextoPesos.Text = carrito.TotalPeso.ToString("N0", Formato);
            TextoBolivares.Text = carrito.TotalBs.ToString("N2", Formato) + " Bs";
            TextoDolares.Text = "$" + carrito.TotalUsd.ToString("N2", Formato);

            if (_pideCliente)
            {
                PanelCliente.Visibility = Visibility.Visible;
            }

            if (!esCredito)
            {
                ComboPago.ItemsSource = Opciones();
                ComboPago.SelectedIndex = 0;
            }
            else
            {
                PanelPago.Visibility = Visibility.Collapsed;
                Loaded += (s, e) => CampoCedula.Focus();
            }
        }

        /// <summary>Mismo orden y atajos que el POS web.</summary>
        private static List<OpcionPago> Opciones()
        {
            return new List<OpcionPago>
            {
                new OpcionPago("option1", "(1) Punto"),
                new OpcionPago("option7", "(2) BioPago"),
                new OpcionPago("option6", "(3) Pesos"),
                new OpcionPago("option4", "(4) Efectivo"),
                new OpcionPago("option2", "(5) Pago Móvil"),
                new OpcionPago("option3", "(6) Transferencia"),
                new OpcionPago("option5", "(7) Dólares"),
            };
        }

        private static readonly Dictionary<Key, string> Atajos = new Dictionary<Key, string>
        {
            { Key.D1, "option1" }, { Key.D2, "option7" }, { Key.D3, "option6" }, { Key.D4, "option4" },
            { Key.D5, "option2" }, { Key.D6, "option3" }, { Key.D7, "option5" },
            { Key.NumPad1, "option1" }, { Key.NumPad2, "option7" }, { Key.NumPad3, "option6" },
            { Key.NumPad4, "option4" }, { Key.NumPad5, "option2" }, { Key.NumPad6, "option3" },
            { Key.NumPad7, "option5" },
        };

        private void Ventana_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Continuar_Click(sender, null);
                return;
            }

            if (_esCredito || ComboPago.SelectedItem == null)
            {
                return;
            }

            string clave;
            if (Atajos.TryGetValue(e.Key, out clave) && !(Keyboard.FocusedElement is System.Windows.Controls.TextBox))
            {
                foreach (OpcionPago opcion in ComboPago.Items)
                {
                    if (opcion.Clave == clave)
                    {
                        ComboPago.SelectedItem = opcion;
                        break;
                    }
                }
                CampoCedula.Focus();
                e.Handled = true;
            }
        }

        private void CampoCedula_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string cedula = CampoCedula.Text.Trim();
            if (cedula.Length < 6 || _buscarCliente == null)
            {
                return;
            }

            var cliente = _buscarCliente(cedula);
            if (cliente != null)
            {
                CampoNombre.Text = cliente.Nombre;
                CampoTelefono.Text = cliente.Telefono;
            }
        }

        private void Continuar_Click(object sender, RoutedEventArgs e)
        {
            TextoError.Text = "";

            if (!_esCredito)
            {
                var opcion = ComboPago.SelectedItem as OpcionPago;
                if (opcion == null)
                {
                    TextoError.Text = "Selecciona un método de pago.";
                    return;
                }
                MetodoPago = opcion.Clave;
            }
            else
            {
                MetodoPago = "0"; // El POS web usa '0' para crédito.
            }

            if (_pideCliente)
            {
                string cedula = CampoCedula.Text.Trim();
                string nombre = CampoNombre.Text.Trim();
                string telefono = CampoTelefono.Text.Trim();

                if (cedula.Length == 0 || nombre.Length == 0 || telefono.Length == 0)
                {
                    TextoError.Text = "Completa cédula, nombre y teléfono del cliente.";
                    return;
                }

                Cliente = new Cliente { Cedula = cedula, Nombre = nombre, Telefono = telefono };
            }
            else
            {
                Cliente = new Cliente { Cedula = "", Nombre = "", Telefono = "" };
            }

            DialogResult = true;
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    public sealed class OpcionPago
    {
        public string Clave { get; private set; }
        public string Texto { get; private set; }

        public OpcionPago(string clave, string texto)
        {
            Clave = clave;
            Texto = texto;
        }
    }
}
