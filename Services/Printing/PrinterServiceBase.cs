using TolllgaFinale.Models;

namespace TolllgaFinale.Services;

/// <summary>
/// Shared print workflow that stays independent from UI code and platform USB details.
/// It coordinates PDF generation, printer lookup, and transport execution.
/// </summary>
public abstract class PrinterServiceBase : IPrinterService
{
    private readonly IInvoiceService _invoiceService;
    private readonly PrinterSettingsService _printerSettings;
    private readonly IPrinterCatalogService _printerCatalog;
    private readonly IPrinterTransport _transport;

    protected PrinterServiceBase(
        IInvoiceService invoiceService,
        PrinterSettingsService printerSettings,
        IPrinterCatalogService printerCatalog,
        IPrinterTransport transport)
    {
        _invoiceService = invoiceService;
        _printerSettings = printerSettings;
        _printerCatalog = printerCatalog;
        _transport = transport;
    }

    public async Task<PrintResult> PrintWeightRecordAsync(WeightRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var printer = await _printerSettings.GetConfiguredPrinterAsync();
        if (printer is null)
        {
            printer = await _printerCatalog.GetDefaultPrinterAsync(cancellationToken);
            if (printer is not null)
            {
                await _printerSettings.SaveConfiguredPrinterAsync(printer);
            }
        }

        if (printer is null)
            return PrintResult.Fail("Aucune imprimante configurée.");

        try
        {
            var pdfPath = await _invoiceService.GeneratePdfAsync(record);
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
                return PrintResult.Fail("Le PDF généré est introuvable.");

            var pdfBytes = await File.ReadAllBytesAsync(pdfPath, cancellationToken);
            var transportResult = await _transport.SendAsync(pdfBytes, printer, cancellationToken);

            return transportResult.Success
                ? PrintResult.Ok(transportResult.Message, pdfPath, printer.DisplayName)
                : PrintResult.Fail(transportResult.Message);
        }
        catch (OperationCanceledException)
        {
            return PrintResult.Fail("Impression annulée.");
        }
        catch (Exception ex)
        {
            return PrintResult.Fail(ex.Message);
        }
    }
}
