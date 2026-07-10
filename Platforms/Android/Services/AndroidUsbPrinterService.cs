namespace TolllgaFinale.Services;

/// <summary>
/// Android printer service that orchestrates PDF generation and USB delivery.
/// </summary>
public sealed class AndroidUsbPrinterService : PrinterServiceBase
{
    public AndroidUsbPrinterService(
        IInvoiceService invoiceService,
        PrinterSettingsService printerSettings,
        IPrinterCatalogService printerCatalog,
        AndroidUsbPrinterTransport transport)
        : base(invoiceService, printerSettings, printerCatalog, transport)
    {
    }
}
