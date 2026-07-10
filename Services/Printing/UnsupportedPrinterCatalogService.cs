namespace TolllgaFinale.Services;

/// <summary>
/// Fallback catalog for platforms where printer enumeration is not implemented yet.
/// </summary>
public sealed class UnsupportedPrinterCatalogService : IPrinterCatalogService
{
    public Task<IReadOnlyList<PrinterConfiguration>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PrinterConfiguration>>(Array.Empty<PrinterConfiguration>());

    public Task<PrinterConfiguration?> GetDefaultPrinterAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<PrinterConfiguration?>(null);
}
