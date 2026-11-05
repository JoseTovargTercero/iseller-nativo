using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Iseller.Pos.Core.Modelos;
using Iseller.Pos.Core.Precios;
using Iseller.Pos.Core.Utilidades;

namespace Iseller.Pos.App
{
    public partial class VentanaPrecioEspecial : Window
    {
        private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-VE");

        private readonly Producto _producto;
        private readonly CalculadoraPrecios _calculadora;
        private readonly double _precioCompra;

        public PreciosCalculados Precios { get; private set; }

        public VentanaPrecioEspecial(Producto producto, CalculadoraPrecios calculadora, PreciosCalculados actual)
        {
            InitializeComponent();

            _producto = producto;
            _calculadora = calculadora;
            _precioCompra = producto.Mayor == "1"
                ? producto.PrecioCompra
                : (producto.CantidadUnidades > 0
                    ? producto.PrecioCompra / producto.CantidadUnidades
                    : producto.PrecioCompra);

            TextoProducto.Text = producto.Nombre;
            TextoCompra.Text = "Precio de compra: " + Dinero(_precioCompra);
            TextoActual.Text = "Venta actual: " + Dinero(actual.PrecioVentaDolar);
            CampoPrecio.Text = actual.PrecioVentaDolar.ToString("0.00", Formato);

            Loaded += (s, e) =>
            {
                CampoPrecio.Focus();
                CampoPrecio.SelectAll();
            };
        }

        private void CampoPrecio_TextChanged(object sender, TextChangedEventArgs e)
        {
            Previsualizar();
        }

        private void CampoPrecio_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Aplicar();
            }
        }

        private bool Previsualizar()
        {
            double usd = Numero.ADouble(CampoPrecio.Text);

            if (usd <= 0)
            {
                TextoError.Text = "Escribe un precio válido.";
                LimpiarVista();
                return false;
            }
            if (_precioCompra > 0 && usd + 0.000001 < _precioCompra)
            {
                TextoError.Text = "No puede ser menor al precio de compra (" + Dinero(_precioCompra) + ").";
                LimpiarVista();
                return false;
            }

            var precios = _calculadora.CalcularDesdeDolarVenta(ProductoPrecio(), usd);
            PreviewDolar.Text = Dinero(precios.PrecioVentaDolar);
            PreviewPeso.Text = precios.PrecioVentaPeso.ToString("N0", Formato) + " Cop";
            PreviewBs.Text = precios.PrecioVentaBs.ToString("N2", Formato) + " Bs";
            TextoError.Text = "";
            return true;
        }

        private void Aplicar()
        {
            if (!Previsualizar())
            {
                return;
            }

            Precios = _calculadora.CalcularDesdeDolarVenta(ProductoPrecio(), Numero.ADouble(CampoPrecio.Text));
            DialogResult = true;
        }

        private void Aplicar_Click(object sender, RoutedEventArgs e)
        {
            Aplicar();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void LimpiarVista()
        {
            PreviewDolar.Text = "";
            PreviewPeso.Text = "";
            PreviewBs.Text = "";
        }

        private ProductoPrecio ProductoPrecio()
        {
            return new ProductoPrecio
            {
                CantidadUnidades = _producto.CantidadUnidades,
                Origen = _producto.Origen,
                PrecioCompra = _producto.PrecioCompra,
                Porcentaje = _producto.Porcentaje,
                Mayor = _producto.Mayor,
            };
        }

        private static string Dinero(double valor)
        {
            return "$" + valor.ToString("N2", Formato);
        }
    }
}
