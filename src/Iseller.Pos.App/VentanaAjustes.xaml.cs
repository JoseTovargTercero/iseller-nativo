using System;
using System.Collections.Generic;
using System.Windows;
using Iseller.Pos.Core.Configuracion;
using Iseller.Pos.Core.Impresion;
using Iseller.Pos.Core.Modelos;
using Newtonsoft.Json;

namespace Iseller.Pos.App
{
    public partial class VentanaAjustes : Window
    {
        private readonly AppConfig _config;

        public VentanaAjustes(AppConfig config)
        {
            InitializeComponent();
            _config = config;

            Ancho58.IsChecked = config.AnchoMm < 80;
            Ancho80.IsChecked = config.AnchoMm >= 80;
            CheckCajon.IsChecked = config.AbrirCajon ?? true;
            Refrescar();
        }

        private void Refrescar_Click(object sender, RoutedEventArgs e)
        {
            Refrescar();
        }

        private void Refrescar()
        {
            try
            {
                List<string> impresoras = ImpresoraRaw.Instaladas();
                ComboImpresoras.ItemsSource = impresoras;
                if (!string.IsNullOrWhiteSpace(_config.Impresora) && impresoras.Contains(_config.Impresora))
                {
                    ComboImpresoras.SelectedItem = _config.Impresora;
                }
                else if (impresoras.Count > 0)
                {
                    ComboImpresoras.SelectedIndex = 0;
                }
                TextoEstado.Text = impresoras.Count + " impresora(s) instalada(s).";
            }
            catch (Exception ex)
            {
                TextoEstado.Text = "No se pudieron listar las impresoras: " + ex.Message;
            }
        }

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            Volcar();
            _config.Guardar();
            DialogResult = true;
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Probar_Click(object sender, RoutedEventArgs e)
        {
            Volcar();
            try
            {
                var venta = new Venta
                {
                    IdPedido = "prueba",
                    FechaLocal = DateTimeOffset.Now,
                    MetodoPago = "option4",
                    Despacho = 1,
                    UsuarioNombre = "Prueba de impresora",
                    ProductosJson = JsonConvert.SerializeObject(new List<ItemCarrito>
                    {
                        new ItemCarrito { Id = 1, Name = "Producto de prueba", Qty = 1, Price = 1, PricePeso = 100, PriceBolivar = 100, CantidadPaca = 1 },
                    }),
                };

                ImpresoraRaw.Enviar(_config.Impresora, TicketEscPos.Generar(venta, "PRUEBA", _config.ComoOpcionesImpresion()));
                TextoEstado.Text = "Ticket de prueba enviado a \"" + _config.Impresora + "\".";
            }
            catch (Exception ex)
            {
                TextoEstado.Text = "No se pudo imprimir: " + ex.Message;
            }
        }

        private void Volcar()
        {
            // La URL y la clave de la API no se editan desde aquí: van en config.json.
            _config.Impresora = ComboImpresoras.SelectedItem as string ?? "";
            _config.AnchoMm = Ancho80.IsChecked == true ? 80 : 58;
            _config.AbrirCajon = CheckCajon.IsChecked == true;
        }
    }
}
