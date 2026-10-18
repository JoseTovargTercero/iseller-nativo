using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace Iseller.Pos.Core.Impresion
{
    /// <summary>
    /// Manda bytes crudos (ESC/POS) a una impresora de Windows por el spooler.
    /// Si la impresora no existe o está apagada, lanza excepción; el llamador decide
    /// si eso impide cobrar (no debe impedirlo).
    /// </summary>
    public static class ImpresoraRaw
    {
        public static List<string> Instaladas()
        {
            var nombres = new List<string>();
            foreach (string nombre in PrinterSettings.InstalledPrinters)
            {
                nombres.Add(nombre);
            }
            return nombres;
        }

        public static void Enviar(string impresora, byte[] datos)
        {
            if (string.IsNullOrWhiteSpace(impresora))
            {
                throw new InvalidOperationException("No hay impresora configurada.");
            }
            if (datos == null || datos.Length == 0)
            {
                return;
            }

            IntPtr handle = IntPtr.Zero;
            try
            {
                if (!OpenPrinter(impresora, out handle, IntPtr.Zero))
                {
                    throw new InvalidOperationException("No se pudo abrir la impresora \"" + impresora + "\".");
                }

                var info = new DOC_INFO_1
                {
                    pDocName = "iSeller POS",
                    pDatatype = "RAW",
                    pOutputFile = null,
                };

                if (StartDocPrinter(handle, 1, ref info) == 0)
                {
                    throw new InvalidOperationException("No se pudo iniciar el documento de impresión.");
                }

                try
                {
                    if (!StartPagePrinter(handle))
                    {
                        throw new InvalidOperationException("No se pudo iniciar la página de impresión.");
                    }
                    try
                    {
                        int escritos;
                        if (!WritePrinter(handle, datos, datos.Length, out escritos) || escritos != datos.Length)
                        {
                            throw new InvalidOperationException("La impresora no aceptó todos los datos.");
                        }
                    }
                    finally
                    {
                        EndPagePrinter(handle);
                    }
                }
                finally
                {
                    EndDocPrinter(handle);
                }
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    ClosePrinter(handle);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DOC_INFO_1
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPWStr)] public string pDatatype;
            [MarshalAs(UnmanagedType.LPWStr)] public string pOutputFile;
        }

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool OpenPrinter(string nombre, out IntPtr handle, IntPtr defaults);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ClosePrinter(IntPtr handle);

        [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int StartDocPrinter(IntPtr handle, int nivel, ref DOC_INFO_1 info);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr handle);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr handle);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr handle);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr handle, byte[] datos, int cantidad, out int escritos);
    }
}
