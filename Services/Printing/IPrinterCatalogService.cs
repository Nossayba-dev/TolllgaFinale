namespace TolllgaFinale.Services;

/// <summary>
/// Provides available printers for administration and a default printer fallback.
/// This keeps printer discovery separate from printing and PDF generation.
/// </summary>
public interface IPrinterCatalogService
{
    Task<IReadOnlyList<PrinterConfiguration>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default);
    Task<PrinterConfiguration?> GetDefaultPrinterAsync(CancellationToken cancellationToken = default);
}
