using System;
using System.Globalization;
using System.Windows;

namespace Iseller.Pos.App
{
    public partial class VentanaCambio : Window
    {
        private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-VE");

        private readonly double _totalPesos;
        private readonly double _totalDolares;
        private readonly double _totalBolivares;

        public VentanaCambio(double totalPesos, double totalDolares, double totalBolivares)
        {
            InitializeComponent();

            _totalPesos = Math.Round(totalPesos);
            _totalDolares = totalDolares;
            _totalBolivares = totalBolivares;

            Cambio(0);
            Loaded += (s, e) => CampoRecibido.Focus();
        }

        private void CampoRecibido_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            double recibido;
            if (!double.TryParse(CampoRecibido.Text.Replace(",", "."), NumberStyles.Float,
                                 CultureInfo.InvariantCulture, out recibido))
            {
                Cambio(0);
                return;
            }
            Cambio(recibido);
        }

        private void Cambio(double recibido)
        {
            TextoPesos.Text = (recibido - _totalPesos).ToString("N0", Formato);
            TextoDolares.Text = "$" + (recibido - _totalDolares).ToString("N2", Formato);
            TextoBolivares.Text = (recibido - _totalBolivares).ToString("N2", Formato) + " Bs";
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
