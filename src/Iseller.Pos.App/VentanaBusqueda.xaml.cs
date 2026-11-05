using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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
        private readonly FiltroCatalogo _filtro;
        private readonly bool _cerrarAlAgregar;
        private readonly ObservableCollection<ProductoVista> _resultados = new ObservableCollection<ProductoVista>();

        // Antiduplicado del lector: tras un escaneo se ignora el mismo código durante 2 s.
        private readonly DispatcherTimer _relojEscaneo = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2),
        };

        private string _ultimoEscaneo;
        private DateTime _bloqueoEscaneo = DateTime.MinValue;

        public VentanaBusqueda(CatalogoRepositorio catalogo, ImagenCache imagenes,
                               CalculadoraPrecios calculadora, Action<Producto, double> alAgregar,
                               FiltroCatalogo filtro = FiltroCatalogo.Todos, string codigoInicial = null)
        {
            InitializeComponent();

            _catalogo = catalogo;
            _imagenes = imagenes;
            _calculadora = calculadora;
            _alAgregar = alAgregar;
            _filtro = filtro;
            _cerrarAlAgregar = !string.IsNullOrWhiteSpace(codigoInicial);

            TablaResultados.ItemsSource = _resultados;
            new LectorBarras(this, CodigoLeido);

            _relojEscaneo.Tick += (s, e) =>
            {
                _relojEscaneo.Stop();
                RelojEscaneo.Visibility = Visibility.Collapsed;
            };

            if (_cerrarAlAgregar)
            {
                string codigo = codigoInicial.Trim();
                CampoBusqueda.Text = codigo;
                TextoEstado.Text = "Escaneado: " + codigo +
                                   " · Enter o AGREGAR para sumarlo al carrito.";
                // Solo el reloj: el bloqueo empieza al agregar, para que el Enter del cajero funcione.
                MostrarReloj();
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
            var porCodigo = _catalogo.BuscarPorCodigo(termino, _filtro);
            if (porCodigo != null)
            {
                // Doble disparo del lector: el mismo código dentro de los 2 s no suma otra unidad.
                if (termino == _ultimoEscaneo && DateTime.Now < _bloqueoEscaneo)
                {
                    CampoBusqueda.Text = "";
                    return;
                }

                MarcarEscaneo(termino);
                Agregar(porCodigo, 1);
                return;
            }

            if (_resultados.Count == 1)
            {
                Agregar(_resultados[0].Modelo, Core.Utilidades.Numero.ADouble(_resultados[0].CantidadTexto));
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
            // Los lectores baratos disparan dos veces: el segundo disparo se ignora.
            if (codigo == _ultimoEscaneo && DateTime.Now < _bloqueoEscaneo)
            {
                return;
            }
            MarcarEscaneo(codigo);

            CampoBusqueda.Text = codigo;
            CampoBusqueda.Focus();
            CampoBusqueda.CaretIndex = CampoBusqueda.Text.Length;
            TextoEstado.Text = "Escaneado: " + codigo + " · Enter o AGREGAR para sumarlo al carrito.";
        }

        /// <summary>Deja el reloj 2 s en la barra: durante ese tiempo se ignora el mismo código.</summary>
        private void MarcarEscaneo(string codigo)
        {
            _ultimoEscaneo = codigo;
            _bloqueoEscaneo = DateTime.Now.AddSeconds(2);
            MostrarReloj();
        }

        private void MostrarReloj()
        {
            RelojEscaneo.Visibility = Visibility.Visible;
            _relojEscaneo.Stop();
            _relojEscaneo.Start();
        }

        private void Cantidad_Menos(object sender, RoutedEventArgs e)
        {
            CambiarCantidad(sender, -1);
        }

        private void Cantidad_Mas(object sender, RoutedEventArgs e)
        {
            CambiarCantidad(sender, 1);
        }

        private void CambiarCantidad(object sender, int delta)
        {
            var vista = (sender as FrameworkElement)?.DataContext as ProductoVista;
            if (vista == null)
            {
                return;
            }

            double actual = Core.Utilidades.Numero.ADouble(vista.CantidadTexto);
            double nueva = Math.Max(1, actual + delta);
            vista.CantidadTexto = nueva.ToString("0.####", CultureInfo.InvariantCulture);
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

            var productos = _catalogo.Buscar(limpio, Maximo, _filtro);
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
