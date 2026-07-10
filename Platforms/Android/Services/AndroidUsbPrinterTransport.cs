using Android.Content;
using Android.Hardware.Usb;

namespace TolllgaFinale.Services;

/// <summary>
/// Android USB transport that sends raw print bytes to the printer through USB Host APIs.
/// Permission handling is delegated to <see cref="IUsbPermissionService"/>.
/// </summary>
public sealed class AndroidUsbPrinterTransport : IPrinterTransport
{
    private const int MaxChunkSize = 16 * 1024;
    private readonly IUsbPermissionService _permissionService;

    public AndroidUsbPrinterTransport(IUsbPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public async Task<PrintResult> SendAsync(byte[] data, PrinterConfiguration printer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(printer);

        if (printer.VendorId is null || printer.ProductId is null)
            return PrintResult.Fail("La configuration USB Android est incomplète.");

        var manager = GetUsbManager();
        if (manager is null)
            return PrintResult.Fail("Le gestionnaire USB Android est indisponible.");

        var device = FindDevice(manager, printer);
        if (device is null)
            return PrintResult.Fail("Imprimante USB Android introuvable.");

        if (!manager.HasPermission(device))
        {
            var granted = await _permissionService.EnsurePermissionAsync(device.VendorId, device.ProductId, cancellationToken);
            if (!granted)
                return PrintResult.Fail("Permission USB refusée pour l'imprimante Android.");
        }

        using var connection = manager.OpenDevice(device);
        if (connection is null)
            return PrintResult.Fail("Impossible d'ouvrir la connexion USB Android.");

        var iface = FindPrinterInterface(device);
        if (iface is null)
            return PrintResult.Fail("Aucune interface USB exploitable n'a été trouvée sur l'imprimante.");

        if (!connection.ClaimInterface(iface, true))
            return PrintResult.Fail("Impossible de prendre le contrôle de l'interface USB.");

        try
        {
            var endpoint = FindOutEndpoint(iface);
            if (endpoint is null)
                return PrintResult.Fail("Aucun endpoint de sortie USB n'a été trouvé sur l'imprimante.");

            for (var offset = 0; offset < data.Length; offset += MaxChunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var size = Math.Min(MaxChunkSize, data.Length - offset);
                var chunk = new byte[size];
                Buffer.BlockCopy(data, offset, chunk, 0, size);

                var written = connection.BulkTransfer(endpoint, chunk, size, 5000);
                if (written < 0 || written != size)
                    return PrintResult.Fail("La transmission USB Android a échoué.");
            }
        }
        finally
        {
            connection.ReleaseInterface(iface);
        }

        return PrintResult.Ok("Document envoyé à l'imprimante Android.", printerName: printer.DisplayName);
    }

    private static UsbManager? GetUsbManager()
        => Android.App.Application.Context.GetSystemService(Context.UsbService) as UsbManager;

    private static UsbDevice? FindDevice(UsbManager manager, PrinterConfiguration printer)
    {
        foreach (var device in manager.DeviceList.Values)
        {
            if (printer.VendorId is not null && device.VendorId != printer.VendorId)
                continue;

            if (printer.ProductId is not null && device.ProductId != printer.ProductId)
                continue;

            if (!string.IsNullOrWhiteSpace(printer.DeviceName) && !string.Equals(device.DeviceName, printer.DeviceName, StringComparison.OrdinalIgnoreCase))
                continue;

            return device;
        }

        return null;
    }

    private static UsbInterface? FindPrinterInterface(UsbDevice device)
    {
        for (var i = 0; i < device.InterfaceCount; i++)
        {
            var iface = device.GetInterface(i);
            if (FindOutEndpoint(iface) is not null)
                return iface;
        }

        return null;
    }

    private static UsbEndpoint? FindOutEndpoint(UsbInterface iface)
    {
        for (var i = 0; i < iface.EndpointCount; i++)
        {
            var endpoint = iface.GetEndpoint(i);
            if (endpoint.Direction == UsbAddressing.Out)
                return endpoint;
        }

        return null;
    }
}
