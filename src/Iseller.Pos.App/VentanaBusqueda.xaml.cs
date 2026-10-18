using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Iseller.Pos.Core.Datos;
using Iseller.Pos.Core.Imagenes;
using Iseller.Pos.Core.Modelos;
using Iseller.Pos.Core.Precios;

namespace Iseller.Pos.App
{
    /// <summary>
    /// Buscador modal del POS (equivalente al modal del POS web): el nombre del producto
    /// se ve completo y desde aquí se agrega al carrito.
    /// </summary>
    public partial class VentanaBusqueda : Window
    {
        private const int Maximo = 100;
        private const int MaximoMiniaturas = 40;

        private readonly CatalogoRepositorio _catalogo;
        private readonly ImagenCache _imagenes;
        private readonly CalculadoraPrecios _calculadora;
        private readonly Action<Producto, double> _alAgregar;
        private readonly ObservableCollection<ProductoVista> _resultados = new ObservableCollection<ProductoVista>();

        public VentanaBusqueda(CatalogoRepositorio catalogo, ImagenCache imagenes,
                               CalculadoraPrecios calculadora, Action<Producto, double> alAgregar)
        {
            InitializeComponent();

            _catalogo = catalogo;
            _imagenes = imagenes;
            _calculadora = calculadora;
            _alAgregar = alAgregar;

            TablaResultados.ItemsSource = _resultados;
            new LectorBarras(this, CodigoLeido);
            Loaded += (s, e) => CampoBusqueda.Focus();
        }

        /// <summary>Código escaneado con el foco fuera del buscador: se agrega igual que con Enter.</summary>
        private void CodigoLeido(string codigo)
        {
            var producto = _catalogo.BuscarPorCodigo(codigo);
            if (producto == null)
            {
                TextoEstado.Text = "El producto con código " + codigo + " no está en el catálogo.";
                return;
            }
            Agregar(producto, 1);
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

        private void CampoBusqueda_TextChanged(object sender, TextChangedEventArgs e)
        {
            Buscar(CampoBusqueda.Text);
        }

        private void CampoBusqueda_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
                return;
            }

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
                Agregar(_resultados[0].Modelo, Core.Utilidades.Numero.ADouble(_resultados[0].CantidadTexto));
            }
        }

        private void TablaResultados_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var vista = TablaResultados.SelectedItem as ProductoVista;
            if (vista != null)
            {
                Agregar(vista.Modelo, Core.Utilidades.Numero.ADouble(vista.CantidadTexto));
            }
        }

        private void Agregar_Click(object sender, RoutedEventArgs e)
        {
            var vista = (sender as FrameworkElement)?.DataContext as ProductoVista;
            if (vista != null)
            {
                Agregar(vista.Modelo, Core.Utilidades.Numero.ADouble(vista.CantidadTexto));
            }
        }

        private void Agregar(Producto producto, double cantidad)
        {
            if (producto == null)
            {
                return;
            }
            if (cantidad <= 0)
            {
                TextoEstado.Text = "La cantidad debe ser mayor a cero.";
                return;
            }

            if (_alAgregar != null)
            {
                _alAgregar(producto, cantidad);
            }

            TextoEstado.Text = "Agregado: " + producto.Nombre + " x" + cantidad;
            CampoBusqueda.Text = "";
            CampoBusqueda.Focus();
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
                _resultados.Clear();
                MensajeVacio.Visibility = Visibility.Visible;
                TextoResultados.Text = "";
                return;
            }

            var productos = _catalogo.Buscar(limpio, Maximo);
            Mostrar(productos);
            TextoResultados.Text = productos.Count + " resultado(s)";
        }

        private void Mostrar(IEnumerable<Producto> productos)
        {
            _resultados.Clear();
            foreach (var producto in productos)
            {
                _resultados.Add(ProductoVista.Desde(producto, _calculadora));
            }

            MensajeVacio.Visibility = _resultados.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Miniaturas solo de las primeras filas; la caché evita repetir descargas.
            foreach (var vista in _resultados.Take(MaximoMiniaturas))
            {
                _ = vista.CargarImagenAsync(_imagenes);
            }
        }
    }
}
