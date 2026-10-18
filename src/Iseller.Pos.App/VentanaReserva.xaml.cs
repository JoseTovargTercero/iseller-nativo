using System.Windows;

namespace Iseller.Pos.App
{
    public partial class VentanaReserva : Window
    {
        public string Cliente { get; private set; }

        public VentanaReserva()
        {
            InitializeComponent();
            Loaded += (s, e) => CampoCliente.Focus();
        }

        private void Reservar_Click(object sender, RoutedEventArgs e)
        {
            string cliente = (CampoCliente.Text ?? "").Trim();
            if (cliente.Length == 0)
            {
                TextoError.Text = "Escribe un identificador para el carrito.";
                return;
            }

            Cliente = cliente;
            DialogResult = true;
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
