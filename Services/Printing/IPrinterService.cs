using TolllgaFinale.Models;

namespace TolllgaFinale.Services;

/// <summary>
/// Abstraction for all printing operations used by the UI.
/// The UI should only talk to this interface, never to platform-specific code.
/// </summary>
public interface IPrinterService
{
    Task<PrintResult> PrintWeightRecordAsync(WeightRecord record, CancellationToken cancellationToken = default);
}
