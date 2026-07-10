using TolllgaFinale.Models;

namespace TolllgaFinale.Services;

/// <summary>
/// Fallback printer service for platforms that do not provide USB printing yet.
/// </summary>
public sealed class UnsupportedPrinterService : IPrinterService
{
    public Task<PrintResult> PrintWeightRecordAsync(WeightRecord record, CancellationToken cancellationToken = default)
        => Task.FromResult(PrintResult.Fail("L'impression USB n'est pas prise en charge sur cette plateforme."));
}
