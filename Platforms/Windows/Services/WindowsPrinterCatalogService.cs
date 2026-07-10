using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;

namespace TolllgaFinale.Services;

/// <summary>
/// Windows printer catalog used by administration screens and default printer fallback.
/// </summary>
public sealed class WindowsPrinterCatalogService : IPrinterCatalogService
{
    private const uint PrinterEnumLocal = 0x00000002;
    private const uint PrinterEnumConnections = 0x00000004;

    public Task<IReadOnlyList<PrinterConfiguration>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default)
    {
        var printers = new List<PrinterConfiguration>();
        if (!EnumPrinters(PrinterEnumLocal | PrinterEnumConnections, null, 4, IntPtr.Zero, 0, out var needed, out var returned)
            && needed > 0)
        {
            var buffer = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (EnumPrinters(PrinterEnumLocal | PrinterEnumConnections, null, 4, buffer, needed, out _, out returned))
                {
                    var size = Marshal.SizeOf<PrinterInfo4>();
                    for (var i = 0; i < returned; i++)
                    {
                        var current = buffer + (i * size);
                        var info = Marshal.PtrToStructure<PrinterInfo4>(current);
                        if (!string.IsNullOrWhiteSpace(info.pPrinterName))
                            printers.Add(CreateConfiguration(info.pPrinterName));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return Task.FromResult<IReadOnlyList<PrinterConfiguration>>(printers);
    }

    public async Task<PrinterConfiguration?> GetDefaultPrinterAsync(CancellationToken cancellationToken = default)
    {
        var defaultName = GetDefaultPrinterName();
        if (string.IsNullOrWhiteSpace(defaultName))
            return null;

        var printers = await GetAvailablePrintersAsync(cancellationToken).ConfigureAwait(false);
        var match = printers.FirstOrDefault(p => string.Equals(p.DisplayName, defaultName, StringComparison.OrdinalIgnoreCase));
        return match ?? CreateConfiguration(defaultName);
    }

    private static PrinterConfiguration CreateConfiguration(string printerName)
        => new()
        {
            PrinterId = printerName,
            DisplayName = printerName,
            DeviceName = printerName,
            TransportKind = PrinterTransportKind.Usb
        };

    private static string? GetDefaultPrinterName()
    {
        var capacity = 256;
        var sb = new System.Text.StringBuilder(capacity);
        return GetDefaultPrinter(sb, ref capacity) ? sb.ToString() : null;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PrinterInfo4
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string? pPrinterName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pServerName;
        public uint Attributes;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumPrinters(uint flags, string? name, uint level, IntPtr pPrinterEnum, uint cbBuf, out uint pcbNeeded, out uint pcReturned);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetDefaultPrinter(System.Text.StringBuilder pszBuffer, ref int pcchBuffer);
}
