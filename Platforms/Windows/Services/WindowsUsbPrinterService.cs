namespace TolllgaFinale.Services;

/// <summary>
/// Windows printer service that keeps the UI agnostic from PDF generation and USB delivery.
/// </summary>
public sealed class WindowsUsbPrinterService : PrinterServiceBase
{
    public WindowsUsbPrinterService(
        IInvoiceService invoiceService,
        PrinterSettingsService printerSettings,
        IPrinterCatalogService printerCatalog,
        WindowsUsbPrinterTransport transport)
        : base(invoiceService, printerSettings, printerCatalog, transport)
    {
    }
}
