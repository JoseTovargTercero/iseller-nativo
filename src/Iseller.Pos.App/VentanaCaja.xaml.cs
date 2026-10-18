using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace Iseller.Pos.App
{
    public partial class VentanaCaja : Window
    {
        private readonly bool _esCierre;

        public Dictionary<string, double> Montos { get; private set; }
        public string Observaciones { get; private set; }

        public VentanaCaja(bool esCierre)
        {
            InitializeComponent();

            _esCierre = esCierre;
            Montos = new Dictionary<string, double>();

            if (esCierre)
            {
                Title = "Cierre de caja";
                TextoTitulo.Text = "Cierre de caja";
                TextoSubtitulo.Text = "Indica el dinero contado y el fondo con el que queda la caja.";
                PanelApertura.Visibility = Visibility.Collapsed;
                PanelCierre.Visibility = Visibility.Visible;
                BotonConfirmar.Content = "REALIZAR CIERRE";
                Loaded += (s, e) => CierreBs.Focus();
            }
            else
            {
                Title = "Apertura de caja";
                TextoTitulo.Text = "Apertura de caja";
                TextoSubtitulo.Text = "Registra el fondo inicial con el que empieza el turno.";
                BotonConfirmar.Content = "REALIZAR APERTURA";
                Loaded += (s, e) => AperturaBs.Focus();
            }
        }

        private void Confirmar_Click(object sender, RoutedEventArgs e)
        {
            TextoError.Text = "";

            if (_esCierre)
            {
                Montos["efectivo_bs_contado"] = Numero(CierreBs.Text);
                Montos["efectivo_usd_contado"] = Numero(CierreUsd.Text);
                Montos["pesos_contado"] = Numero(CierrePesos.Text);
                Montos["punto_contado"] = Numero(CierrePunto.Text);
                Montos["biopago_contado"] = Numero(CierreBioPago.Text);
                Montos["pago_movil_contado"] = Numero(CierrePagoMovil.Text);
                Montos["transferencia_contado"] = Numero(CierreTransferencia.Text);
                Montos["efectivo_bs_fondo"] = Numero(CierreFondoBs.Text);
                Montos["efectivo_usd_fondo"] = Numero(CierreFondoUsd.Text);
                Montos["pesos_fondo"] = Numero(CierreFondoPesos.Text);
            }
            else
            {
                Montos["efectivo_bs_fondo"] = Numero(AperturaBs.Text);
                Montos["efectivo_usd_fondo"] = Numero(AperturaUsd.Text);
                Montos["pesos_fondo"] = Numero(AperturaPesos.Text);

                if (Montos["efectivo_bs_fondo"] <= 0 && Montos["efectivo_usd_fondo"] <= 0 &&
                    Montos["pesos_fondo"] <= 0)
                {
                    TextoError.Text = "Ingresa al menos un monto de fondo para abrir la caja.";
                    return;
                }
            }

            Observaciones = (CampoObservaciones.Text ?? "").Trim();
            DialogResult = true;
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private static double Numero(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return 0;
            }

            double valor;
            string limpio = texto.Trim().Replace(",", ".");
            return double.TryParse(limpio, NumberStyles.Float, CultureInfo.InvariantCulture, out valor)
                ? valor
                : 0;
        }
    }
}
