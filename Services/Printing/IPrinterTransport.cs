namespace TolllgaFinale.Services;

/// <summary>
/// Low-level transport used to send print data to a configured device.
/// Platform-specific implementations handle USB, Bluetooth, or network I/O.
/// </summary>
public interface IPrinterTransport
{
    Task<PrintResult> SendAsync(byte[] data, PrinterConfiguration printer, CancellationToken cancellationToken = default);
}
