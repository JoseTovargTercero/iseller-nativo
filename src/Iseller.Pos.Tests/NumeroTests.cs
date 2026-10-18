using Iseller.Pos.Core.Utilidades;
using Xunit;

namespace Iseller.Pos.Tests
{
    public class NumeroTests
    {
        [Theory]
        [InlineData("", true)]
        [InlineData("0", true)]
        [InlineData("123", true)]
        [InlineData("12.5", true)]
        [InlineData(".5", true)]
        [InlineData("12.5.6", false)]
        [InlineData("12,5", false)]   // la coma no se escribe: se escribe el punto
        [InlineData("12a", false)]
        [InlineData("1 000", false)]
        [InlineData("-5", false)]
        [InlineData("1e3", false)]
        [InlineData("$10", false)]
        public void Solo_acepta_digitos_y_un_punto(string texto, bool esperado)
        {
            Assert.Equal(esperado, Numero.EsValido(texto, true));
        }

        [Theory]
        [InlineData("123", true)]
        [InlineData("12.5", false)]
        [InlineData("", true)]
        public void En_modo_entero_no_acepta_punto(string texto, bool esperado)
        {
            Assert.Equal(esperado, Numero.EsValido(texto, false));
        }

        [Theory]
        [InlineData("12.5", 12.5)]
        [InlineData("12,5", 12.5)]
        [InlineData(" 7 ", 7)]
        [InlineData("", 0)]
        [InlineData("abc", 0)]
        public void Convierte_el_texto_a_numero(string texto, double esperado)
        {
            Assert.Equal(esperado, Numero.ADouble(texto), 6);
        }
    }
}
