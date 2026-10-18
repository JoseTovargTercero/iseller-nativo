using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Iseller.Pos.Core.Utilidades;

namespace Iseller.Pos.App
{
    /// <summary>
    /// Restringe un TextBox a números (enteros o con un único punto decimal).
    /// Uso en XAML:  app:Numerico.Decimales="True"  /  app:Numerico.Enteros="True"
    /// </summary>
    public static class Numerico
    {
        public static readonly DependencyProperty DecimalesProperty = DependencyProperty.RegisterAttached(
            "Decimales", typeof(bool), typeof(Numerico), new PropertyMetadata(false, AlCambiar));

        public static readonly DependencyProperty EnterosProperty = DependencyProperty.RegisterAttached(
            "Enteros", typeof(bool), typeof(Numerico), new PropertyMetadata(false, AlCambiar));

        public static void SetDecimales(DependencyObject obj, bool valor)
        {
            obj.SetValue(DecimalesProperty, valor);
        }

        public static bool GetDecimales(DependencyObject obj)
        {
            return (bool)obj.GetValue(DecimalesProperty);
        }

        public static void SetEnteros(DependencyObject obj, bool valor)
        {
            obj.SetValue(EnterosProperty, valor);
        }

        public static bool GetEnteros(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnterosProperty);
        }

        private static void AlCambiar(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var caja = obj as TextBox;
            if (caja == null)
            {
                return;
            }

            caja.PreviewTextInput -= AlEscribir;
            DataObject.RemovePastingHandler(caja, AlPegar);

            if ((bool)e.NewValue)
            {
                caja.PreviewTextInput += AlEscribir;
                DataObject.AddPastingHandler(caja, AlPegar);
            }
        }

        private static void AlEscribir(object sender, TextCompositionEventArgs e)
        {
            var caja = (TextBox)sender;
            e.Handled = !Numero.EsValido(TextoFuturo(caja, e.Text), GetDecimales(caja));
        }

        private static void AlPegar(object sender, DataObjectPastingEventArgs e)
        {
            var caja = (TextBox)sender;
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            string texto = (string)e.DataObject.GetData(DataFormats.Text);
            if (!Numero.EsValido(TextoFuturo(caja, texto), GetDecimales(caja)))
            {
                e.CancelCommand();
            }
        }

        private static string TextoFuturo(TextBox caja, string agregado)
        {
            string actual = caja.Text ?? "";
            int inicio = Math.Min(caja.SelectionStart, actual.Length);
            int largo = Math.Min(caja.SelectionLength, Math.Max(0, actual.Length - inicio));
            return actual.Remove(inicio, largo).Insert(inicio, agregado ?? "");
        }
    }
}
