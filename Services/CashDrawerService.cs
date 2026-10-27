using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.InteropServices;

namespace BakerScaleConnect.Services
{
    /// <summary>
    /// Opens the cash drawer. The drawer is wired to the receipt printer's drawer port, so the kick is
    /// an ESC/POS command sent to the printer: either straight down a COM port, or as a raw job through
    /// a Windows print queue (a USB printer with its Windows driver, such as the Citizen CT-S310II).
    /// Going through the queue keeps the kick in line with the receipts printed on the same queue.
    /// </summary>
    public static class CashDrawerService
    {
        // Standard ESC/POS cash drawer kick: ESC p m t1 t2 (drawer pin 2, 50 ms on, 500 ms off)
        private static readonly byte[] KickCommand = [0x1B, 0x70, 0x00, 0x19, 0xFA];

        /// <summary>
        /// Sends the kick to the configured printer or serial port and returns which one it went to.
        /// </summary>
        public static string Kick(AppSettings.CashDrawerConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config.PrinterName))
            {
                SendToPrinter(config.PrinterName);
                return config.PrinterName;
            }

            if (!string.IsNullOrWhiteSpace(config.SerialPort))
            {
                SendToSerialPort(config.SerialPort);
                return config.SerialPort;
            }

            throw new InvalidOperationException("No cash drawer printer or serial port configured.");
        }

        private static void SendToSerialPort(string portName)
        {
            using var port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
            {
                WriteTimeout = 1000,
                ReadTimeout = 1000
            };
            port.Open();
            port.Write(KickCommand, 0, KickCommand.Length);
        }

        private static void SendToPrinter(string printerName)
        {
            if (!OpenPrinter(printerName, out IntPtr printer, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not open printer \"{printerName}\".");

            try
            {
                // RAW skips the driver's rendering, so the printer gets the bytes as they are.
                var docInfo = new DocInfo1 { DocName = "Cash Drawer Kick", Datatype = "RAW" };
                if (StartDocPrinter(printer, 1, docInfo) == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Printer \"{printerName}\" refused a raw job.");

                try
                {
                    if (!StartPagePrinter(printer))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    bool written = WritePrinter(printer, KickCommand, KickCommand.Length, out int count);
                    int error = Marshal.GetLastWin32Error();
                    EndPagePrinter(printer);
                    if (!written || count != KickCommand.Length)
                        throw new Win32Exception(error, $"Could not write to printer \"{printerName}\".");
                }
                finally
                {
                    EndDocPrinter(printer);
                }
            }
            finally
            {
                ClosePrinter(printer);
            }
        }

        #region winspool

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private class DocInfo1
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string DocName = "";
            [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string Datatype = "";
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool OpenPrinter(string printerName, out IntPtr printer, IntPtr defaults);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr printer);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int StartDocPrinter(IntPtr printer, int level, [In] DocInfo1 docInfo);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr printer, byte[] bytes, int count, out int written);

        #endregion
    }
}
