namespace TolllgaFinale.Services;

/// <summary>
/// Describes a saved printer configuration that can be reused for future print jobs.
/// </summary>
public sealed class PrinterConfiguration
{
    public string PrinterId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public PrinterTransportKind TransportKind { get; set; } = PrinterTransportKind.Usb;
    public int? VendorId { get; set; }
    public int? ProductId { get; set; }
    public string? DeviceName { get; set; }
}

/// <summary>
/// Identifies the transport used to reach the printer.
/// The model is intentionally extensible for future Bluetooth or network printers.
/// </summary>
public enum PrinterTransportKind
{
    Usb = 0,
    Bluetooth = 1,
    Network = 2
}
