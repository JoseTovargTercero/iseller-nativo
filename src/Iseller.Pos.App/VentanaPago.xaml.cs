using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Iseller.Pos.Core.Carrito;
using Iseller.Pos.Core.Modelos;
using Iseller.Pos.Core.Utilidades;
namespace Iseller.Pos.App
{
    public partial class VentanaPago : Window
    {
        private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-VE");

        private readonly bool _esCredito;
        private readonly bool _pideCliente;
        private readonly Func<string, Cliente> _buscarCliente;
        private readonly double _totalUsd;
        private readonly double _totalCop;
        private readonly double _totalBs;

        private int _paso = 1;
        private Cliente _cliente;
        private bool _actualizando;

        public string MetodoPago { get; private set; }
        public Cliente Cliente { get; private set; }

        /// <summary>Desglose neto del pago fraccionado (solo con option8).</summary>
        public Dictionary<string, double> PagosFraccionados { get; private set; }

        public VentanaPago(bool esCredito, int registroClientes, Carrito carrito, Func<string, Cliente> buscarCliente)
        {
            InitializeComponent();

            _esCredito = esCredito;
            _pideCliente = esCredito || registroClientes == 1;
            _buscarCliente = buscarCliente;

            // El fraccionado usa los totales sin redondear por línea, igual que el POS web.
            foreach (var item in carrito.Items)
            {
                _totalUsd += item.Price * item.Qty;
                _totalCop += item.PricePeso * item.Qty;
                _totalBs += item.PriceBolivar * item.Qty;
            }

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

        /// <summary>Mismo orden y atajos que el POS web (8 = fraccionado).</summary>
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
                new OpcionPago("option8", "(8) Fraccionado"),
            };
        }

        private static readonly Dictionary<Key, string> Atajos = new Dictionary<Key, string>
        {
            { Key.D1, "option1" }, { Key.D2, "option7" }, { Key.D3, "option6" }, { Key.D4, "option4" },
            { Key.D5, "option2" }, { Key.D6, "option3" }, { Key.D7, "option5" }, { Key.D8, "option8" },
            { Key.NumPad1, "option1" }, { Key.NumPad2, "option7" }, { Key.NumPad3, "option6" },
            { Key.NumPad4, "option4" }, { Key.NumPad5, "option2" }, { Key.NumPad6, "option3" },
            { Key.NumPad7, "option5" }, { Key.NumPad8, "option8" },
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

            if (_esCredito || _paso == 2 || ComboPago.SelectedItem == null)
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

        private void ComboPago_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var opcion = ComboPago.SelectedItem as OpcionPago;
            Wizard.Visibility = opcion != null && opcion.Clave == "option8"
                ? Visibility.Visible
                : Visibility.Collapsed;
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

            if (_paso == 2)
            {
                ConfirmarFraccionado();
                return;
            }

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

                _cliente = new Cliente { Cedula = cedula, Nombre = nombre, Telefono = telefono };
            }
            else
            {
                _cliente = new Cliente { Cedula = "", Nombre = "", Telefono = "" };
            }

            // Con fraccionado, el desglose se pide en el paso 2 (igual que el POS web).
            if (MetodoPago == "option8")
            {
                IrPaso(2);
                return;
            }

            Cliente = _cliente;
            DialogResult = true;
        }

        private void IrPaso(int paso)
        {
            _paso = paso == 2 ? 2 : 1;

            bool fraccionado = _paso == 2;
            PanelPago.Visibility = fraccionado ? Visibility.Collapsed : Visibility.Visible;
            PanelCliente.Visibility = fraccionado ? Visibility.Collapsed : (_pideCliente ? Visibility.Visible : Visibility.Collapsed);
            PanelFraccionado.Visibility = fraccionado ? Visibility.Visible : Visibility.Collapsed;
            BotonAtras.Visibility = fraccionado ? Visibility.Visible : Visibility.Collapsed;
            BotonCancelar.Visibility = fraccionado ? Visibility.Collapsed : Visibility.Visible;
            BotonContinuar.Content = fraccionado ? "CONFIRMAR VENTA" : "CONTINUAR";

            Paso1Texto.FontWeight = fraccionado ? FontWeights.Normal : FontWeights.Bold;
            Paso1Texto.Foreground = (System.Windows.Media.Brush)FindResource(fraccionado ? "MutedBrush" : "TextBrush");
            Paso2Texto.FontWeight = fraccionado ? FontWeights.Bold : FontWeights.Normal;
            Paso2Texto.Foreground = (System.Windows.Media.Brush)FindResource(fraccionado ? "TextBrush" : "MutedBrush");

            if (fraccionado)
            {
                ActualizarFraccionado();
                FracPunto.Focus();
            }
        }

        private void Atras_Click(object sender, RoutedEventArgs e)
        {
            TextoError.Text = "";
            IrPaso(1);
        }

        private void Limpiar_Click(object sender, RoutedEventArgs e)
        {
            _actualizando = true;
            FracPunto.Text = "";
            FracBioPago.Text = "";
            FracPagoMovil.Text = "";
            FracTransferencia.Text = "";
            FracEfectivo.Text = "";
            FracDolares.Text = "";
            FracPesos.Text = "";
            _actualizando = false;
            ActualizarFraccionado();
        }

        private void Fraccion_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_actualizando && _paso == 2)
            {
                ActualizarFraccionado();
            }
        }

        private Dictionary<string, double> LeerPagos()
        {
            return new Dictionary<string, double>
            {
                { CalculadoraFraccionado.Punto, Monto(FracPunto.Text) },
                { CalculadoraFraccionado.BioPago, Monto(FracBioPago.Text) },
                { CalculadoraFraccionado.PagoMovil, Monto(FracPagoMovil.Text) },
                { CalculadoraFraccionado.Transferencia, Monto(FracTransferencia.Text) },
                { CalculadoraFraccionado.Efectivo, Monto(FracEfectivo.Text) },
                { CalculadoraFraccionado.Dolares, Monto(FracDolares.Text) },
                { CalculadoraFraccionado.Pesos, Monto(FracPesos.Text) },
            };
        }

        private CalculadoraFraccionado.Resultado _resultado;

        private void ActualizarFraccionado()
        {
            _resultado = CalculadoraFraccionado.Calcular(LeerPagos(), _totalUsd, _totalCop, _totalBs);

            FaltaCop.Text = _resultado.RestanteCop.ToString("N0", Formato) + " Cop";
            FaltaBs.Text = _resultado.RestanteBs.ToString("N2", Formato) + " Bs";
            FaltaUsd.Text = "$" + _resultado.RestanteUsd.ToString("N2", Formato);

            if (_resultado.Saldado && _resultado.HayExceso)
            {
                EstadoFraccionado.Text = "Cuenta cubierta · Vuelto: " + TextoVuelto();
            }
            else if (_resultado.Saldado)
            {
                EstadoFraccionado.Text = "Cuenta cubierta";
            }
            else
            {
                EstadoFraccionado.Text = "Falta por cubrir la cuenta";
            }
        }

        private string TextoVuelto()
        {
            var partes = new List<string>();
            if (_resultado.ExcesoUsd > 0.01)
            {
                partes.Add("$" + _resultado.ExcesoUsd.ToString("N2", Formato));
            }
            if (_resultado.ExcesoCop > 1)
            {
                partes.Add(_resultado.ExcesoCop.ToString("N0", Formato) + " COP");
            }
            if (_resultado.ExcesoBs > 0.05)
            {
                partes.Add(_resultado.ExcesoBs.ToString("N2", Formato) + " Bs");
            }
            return string.Join(" · ", partes);
        }

        private string ResumenFaltante()
        {
            return _resultado.RestanteCop.ToString("N0", Formato) + " COP · " +
                   _resultado.RestanteBs.ToString("N2", Formato) + " Bs · $" +
                   _resultado.RestanteUsd.ToString("N2", Formato);
        }

        private void ConfirmarFraccionado()
        {
            var pagos = LeerPagos();
            bool hayMonto = false;
            foreach (double valor in pagos.Values)
            {
                if (valor > 0)
                {
                    hayMonto = true;
                    break;
                }
            }

            if (!hayMonto)
            {
                TextoError.Text = "Ingrese al menos un monto para el pago fraccionado.";
                return;
            }

            var resultado = CalculadoraFraccionado.Calcular(pagos, _totalUsd, _totalCop, _totalBs);
            if (!resultado.Saldado)
            {
                _resultado = resultado;
                TextoError.Text = "Pago incompleto. Aún falta por cubrir: " + ResumenFaltante();
                return;
            }

            PagosFraccionados = CalculadoraFraccionado.Netos(resultado);
            Cliente = _cliente;
            DialogResult = true;
        }

        private void Completar_Click(object sender, RoutedEventArgs e)
        {
            var boton = sender as System.Windows.Controls.Button;
            string clave = boton != null ? boton.Tag as string : null;
            if (string.IsNullOrEmpty(clave) || _resultado == null)
            {
                return;
            }

            var campo = CampoDe(clave);
            if (campo == null)
            {
                return;
            }

            double restante = RestanteDe(CalculadoraFraccionado.Moneda(clave));
            if (restante <= 0)
            {
                return;
            }

            // Se suma lo que falta: así este medio cierra el hueco de su moneda aunque
            // otros medios de la misma moneda ya tengan monto, o se haya pagado parte
            // de la cuenta en otra moneda.
            double actual = Monto(campo.Text);
            campo.Text = (actual + restante).ToString("0.##", CultureInfo.InvariantCulture);
            campo.CaretIndex = campo.Text.Length;
            campo.Focus();
        }

        private System.Windows.Controls.TextBox CampoDe(string clave)
        {
            switch (clave)
            {
                case CalculadoraFraccionado.Punto: return FracPunto;
                case CalculadoraFraccionado.BioPago: return FracBioPago;
                case CalculadoraFraccionado.PagoMovil: return FracPagoMovil;
                case CalculadoraFraccionado.Transferencia: return FracTransferencia;
                case CalculadoraFraccionado.Efectivo: return FracEfectivo;
                case CalculadoraFraccionado.Dolares: return FracDolares;
                case CalculadoraFraccionado.Pesos: return FracPesos;
                default: return null;
            }
        }

        private double RestanteDe(string moneda)
        {
            if (moneda == "usd")
            {
                return _resultado.RestanteUsd;
            }
            if (moneda == "cop")
            {
                return _resultado.RestanteCop;
            }
            return _resultado.RestanteBs;
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private static double Monto(string texto)
        {
            return Core.Utilidades.Numero.ADouble(texto);
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
