using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Imagenes;
using Iseller.Pos.Core.Modelos;
using Iseller.Pos.Core.Precios;

namespace Iseller.Pos.App
{
    /// <summary>
    /// Consulta de precios: el cliente escanea un producto y ve su precio e información,
    /// sin pasar por caja. Se cierra solo con la X o Esc (no hay botón de "Atrás").
    /// </summary>
    public partial class VentanaConsultaPrecios : Window
    {
        private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-VE");

        private readonly CatalogoRepositorio _catalogo;
        private readonly ImagenCache _imagenes;
        private readonly CalculadoraPrecios _calculadora;
        private readonly bool _puedeMayor;
        private string _codigoActual;

        public VentanaConsultaPrecios(CatalogoRepositorio catalogo, ImagenCache imagenes,
                                      CalculadoraPrecios calculadora, bool puedeMayor)
        {
            InitializeComponent();

            _catalogo = catalogo;
            _imagenes = imagenes;
            _calculadora = calculadora;
            _puedeMayor = puedeMayor;

            VentanaMaximizada.Aplicar(this);
            new LectorBarras(this, MostrarCodigo);

            Loaded += (s, e) => Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>Código escaneado: busca el producto y muestra su información centrada.</summary>
        private async void MostrarCodigo(string codigo)
        {
            var producto = _catalogo.BuscarPorCodigo(codigo, _puedeMayor);
            if (producto == null)
            {
                PanelEspera.Visibility = Visibility.Collapsed;
                PanelProducto.Visibility = Visibility.Collapsed;
                PanelNoEncontrado.Visibility = Visibility.Visible;
                TextoNoEncontrado.Text = "El código " + codigo + " no está en el catálogo.";
                return;
            }

            var vista = ProductoVista.Desde(producto, _calculadora);
            _codigoActual = codigo;

            NombreProducto.Text = vista.Nombre;
            PrecioDolar.Text = vista.PrecioDolar;
            PrecioBs.Text = vista.PrecioBs;
            PrecioPeso.Text = vista.PrecioPeso;
            DetalleProducto.Text = "Código " + producto.CodigoBarras +
                                   "   ·   " + vista.Stock.ToString("N0", Formato) + " en stock";

            ImagenProducto.Source = null;
            PanelEspera.Visibility = Visibility.Collapsed;
            PanelNoEncontrado.Visibility = Visibility.Collapsed;
            PanelProducto.Visibility = Visibility.Visible;

            await vista.CargarImagenAsync(_imagenes);

            // Si mientras cargaba se escaneó otro producto, no se pisa la imagen nueva.
            if (codigo == _codigoActual)
            {
                ImagenProducto.Source = vista.Imagen;
            }
        }
    }
}
