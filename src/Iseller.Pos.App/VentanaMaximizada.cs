using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Iseller.Pos.App
{
    /// <summary>
    /// Con WindowStyle=None, Windows maximiza a toda la pantalla y tapa la barra de tareas.
    /// Esto limita la ventana maximizada al área de trabajo del monitor donde está.
    /// </summary>
    internal static class VentanaMaximizada
    {
        private const int WmGetMinMaxInfo = 0x0024;
        private const int MonitorDefaultToNearest = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct PuntoEntero
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RectEntero
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct InfoMonitor
        {
            public int Tamanio;
            public RectEntero Monitor;
            public RectEntero Trabajo;
            public int Banderas;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct InfoMinMax
        {
            public PuntoEntero Reservado;
            public PuntoEntero MaximizarTamanio;
            public PuntoEntero MaximizarPosicion;
            public PuntoEntero MinimoArrastre;
            public PuntoEntero MaximoArrastre;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr ventana, int banderas);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref InfoMonitor info);

        public static void Aplicar(Window ventana)
        {
            if (ventana == null)
            {
                return;
            }

            ventana.SourceInitialized += (s, e) =>
            {
                var fuente = PresentationSource.FromVisual(ventana) as HwndSource;
                if (fuente != null)
                {
                    fuente.AddHook(AlRecibirMensaje);
                }
            };
        }

        private static IntPtr AlRecibirMensaje(IntPtr ventana, int mensaje, IntPtr wParam, IntPtr lParam, ref bool manejado)
        {
            if (mensaje != WmGetMinMaxInfo)
            {
                return IntPtr.Zero;
            }

            IntPtr monitor = MonitorFromWindow(ventana, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var infoMonitor = new InfoMonitor { Tamanio = Marshal.SizeOf(typeof(InfoMonitor)) };
            if (!GetMonitorInfo(monitor, ref infoMonitor))
            {
                return IntPtr.Zero;
            }

            var info = (InfoMinMax)Marshal.PtrToStructure(lParam, typeof(InfoMinMax));
            info.MaximizarPosicion.X = infoMonitor.Trabajo.Left - infoMonitor.Monitor.Left;
            info.MaximizarPosicion.Y = infoMonitor.Trabajo.Top - infoMonitor.Monitor.Top;
            info.MaximizarTamanio.X = infoMonitor.Trabajo.Right - infoMonitor.Trabajo.Left;
            info.MaximizarTamanio.Y = infoMonitor.Trabajo.Bottom - infoMonitor.Trabajo.Top;
            Marshal.StructureToPtr(info, lParam, true);

            return IntPtr.Zero;
        }
    }
}
