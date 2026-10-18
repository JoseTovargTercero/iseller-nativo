using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Iseller.Pos.App
{
    /// <summary>
    /// Lector de código de barras por ráfaga de teclado, con el mismo criterio que el POS web:
    /// si entre teclas pasan más de 100 ms, no es un escaneo. Al llegar Enter entrega el código.
    /// Solo acumula cuando el foco no está en un campo de texto, para no pisar lo que se escribe.
    /// </summary>
    public sealed class LectorBarras
    {
        private const int VentanaMs = 100;

        // El POS web solo indexa códigos de más de 5 caracteres; se mantiene el mismo mínimo
        // para no disparar búsquedas con teclas sueltas.
        private const int LargoMinimo = 6;

        private readonly Action<string> _alLeer;
        private readonly StringBuilder _codigo = new StringBuilder();
        private DateTime _ultimaTecla = DateTime.MinValue;

        public LectorBarras(Window ventana, Action<string> alLeer)
        {
            _alLeer = alLeer;
            ventana.PreviewKeyDown += AlTeclear;
        }

        private void AlTeclear(object sender, KeyEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            if (Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is PasswordBox)
            {
                _codigo.Clear();
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Leer();
                return;
            }

            char digito = Digito(e);
            if (digito == '\0')
            {
                return;
            }

            var ahora = DateTime.Now;
            if ((ahora - _ultimaTecla).TotalMilliseconds > VentanaMs)
            {
                _codigo.Clear();
            }
            _ultimaTecla = ahora;
            _codigo.Append(digito);
            e.Handled = true;
        }

        private void Leer()
        {
            string codigo = _codigo.ToString();
            _codigo.Clear();
            if (codigo.Length < LargoMinimo)
            {
                return;
            }
            _alLeer(codigo);
        }

        private static char Digito(KeyEventArgs e)
        {
            if (e.Key >= Key.D0 && e.Key <= Key.D9 && Keyboard.Modifiers == ModifierKeys.None)
            {
                return (char)('0' + (e.Key - Key.D0));
            }
            if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                return (char)('0' + (e.Key - Key.NumPad0));
            }
            return '\0';
        }
    }
}
