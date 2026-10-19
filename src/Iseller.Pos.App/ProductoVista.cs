using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Iseller.Pos.Core.Imagenes;
using Iseller.Pos.Core.Precios;

namespace Iseller.Pos.App
{
    /// <summary>Producto del catálogo con precios e imagen para la vista.</summary>
    public sealed class ProductoVista : INotifyPropertyChanged
    {
        private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-VE");

        private ImageSource _imagen;

        public int Id { get; set; }
        public string Nombre { get; set; }
        public double Stock { get; set; }
        public bool EsCombo { get; set; }
        public string PrecioDolar { get; set; }
        public string PrecioBs { get; set; }
        public string PrecioPeso { get; set; }
        public string Foto { get; set; }

        /// <summary>Cantidad a agregar (texto del campo en la fila; por defecto 1).</summary>
        public string CantidadTexto { get; set; }

        /// <summary>Producto original, para agregarlo al carrito.</summary>
        public Core.Modelos.Producto Modelo { get; set; }

        public ImageSource Imagen
        {
            get { return _imagen; }
            private set
            {
                _imagen = value;
                var handler = PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs("Imagen"));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static ProductoVista Desde(Core.Modelos.Producto p, CalculadoraPrecios calculadora)
        {
            var precios = calculadora.Calcular(new ProductoPrecio
            {
                CantidadUnidades = p.CantidadUnidades,
                Origen = p.Origen,
                PrecioCompra = p.PrecioCompra,
                Porcentaje = p.Porcentaje,
                Mayor = p.Mayor,
            });

            return new ProductoVista
            {
                Id = p.Id,
                Nombre = p.EsCombo == 1 ? p.Nombre + "  [Combo]" : p.Nombre,
                Stock = p.Stock,
                EsCombo = p.EsCombo == 1,
                Modelo = p,
                Foto = p.Foto,
                CantidadTexto = "1",
                PrecioDolar = "$" + Numero(precios.PrecioVentaDolar),
                PrecioBs = Numero(precios.PrecioVentaBs) + " Bs",
                // Los pesos van sin decimales (como el POS web).
                PrecioPeso = precios.PrecioVentaPeso.ToString("N0", Formato) + " Cop",
            };
        }

        /// <summary>Descarga (o toma de la caché) la miniatura sin bloquear la UI.</summary>
        public async Task CargarImagenAsync(ImagenCache cache)
        {
            if (cache == null || string.IsNullOrWhiteSpace(Foto))
            {
                return;
            }

            string ruta = await cache.ObtenerAsync(Id, Foto);
            if (string.IsNullOrEmpty(ruta))
            {
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 96;
                bitmap.UriSource = new Uri(ruta);
                bitmap.EndInit();
                bitmap.Freeze();
                Imagen = bitmap;
            }
            catch (Exception)
            {
                // Imagen corrupta: se ignora.
            }
        }

        private static string Numero(double valor)
        {
            return valor.ToString("N2", Formato);
        }
    }
}
