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
    /// Si se abre con un código leído por el escáner, deja el producto a la vista y espera
    /// el Enter del cajero antes de agregarlo (los lectores baratos disparan dos veces).
    /// </summary>
    public partial class VentanaBusqueda : Window
    {
        private const int Maximo = 100;
        private const int MaximoMiniaturas = 40;

        private readonly CatalogoRepositorio _catalogo;
        private readonly ImagenCache _imagenes;
        private readonly CalculadoraPrecios _calculadora;
        private readonly Action<Producto, double> _alAgregar;
        private readonly bool _puedeMayor;
        private readonly bool _cerrarAlAgregar;
        private readonly ObservableCollection<ProductoVista> _resultados = new ObservableCollection<ProductoVista>();

        public VentanaBusqueda(CatalogoRepositorio catalogo, ImagenCache imagenes,
                               CalculadoraPrecios calculadora, Action<Producto, double> alAgregar,
                               bool puedeMayor = true, string codigoInicial = null)
        {
            InitializeComponent();

            _catalogo = catalogo;
            _imagenes = imagenes;
            _calculadora = calculadora;
            _alAgregar = alAgregar;
            _puedeMayor = puedeMayor;
            _cerrarAlAgregar = !string.IsNullOrWhiteSpace(codigoInicial);

            TablaResultados.ItemsSource = _resultados;
            new LectorBarras(this, CodigoLeido);

            if (_cerrarAlAgregar)
            {
                CampoBusqueda.Text = codigoInicial.Trim();
                TextoEstado.Text = "Escaneado: " + codigoInicial.Trim() +
                                   " · Enter o AGREGAR para sumarlo al carrito.";
            }

            Loaded += (s, e) =>
            {
                CampoBusqueda.Focus();
                CampoBusqueda.CaretIndex = CampoBusqueda.Text.Length;
            };
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
            var porCodigo = _catalogo.BuscarPorCodigo(termino, _puedeMayor);
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

        /// <summary>Código escaneado con el foco fuera del buscador: se escribe y espera el Enter.</summary>
        private void CodigoLeido(string codigo)
        {
            CampoBusqueda.Text = codigo;
            CampoBusqueda.Focus();
            CampoBusqueda.CaretIndex = CampoBusqueda.Text.Length;
            TextoEstado.Text = "Escaneado: " + codigo + " · Enter o AGREGAR para sumarlo al carrito.";
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

            if (_cerrarAlAgregar)
            {
                Close();
                return;
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

            var productos = _catalogo.Buscar(limpio, Maximo, _puedeMayor);
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
