using System.Text.Json;

namespace TolllgaFinale.Services;

/// <summary>
/// Persists and retrieves the printer selected by the administrator.
/// The selected printer is stored locally so future print jobs can reuse it automatically.
/// </summary>
public sealed class PrinterSettingsService
{
    private const string PrinterSettingsKey = "tolllga_printer_settings";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<PrinterConfiguration?> GetConfiguredPrinterAsync()
    {
        try
        {
            var json = Preferences.Default.Get(PrinterSettingsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return Task.FromResult<PrinterConfiguration?>(null);

            var printer = JsonSerializer.Deserialize<PrinterConfiguration>(json, JsonOptions);
            return Task.FromResult<PrinterConfiguration?>(printer);
        }
        catch
        {
            return Task.FromResult<PrinterConfiguration?>(null);
        }
    }

    public async Task SaveConfiguredPrinterAsync(PrinterConfiguration printer)
    {
        ArgumentNullException.ThrowIfNull(printer);
        if (string.IsNullOrWhiteSpace(printer.PrinterId))
            throw new ArgumentException("PrinterId is required.", nameof(printer));

        var json = JsonSerializer.Serialize(printer, JsonOptions);
        Preferences.Default.Set(PrinterSettingsKey, json);
        await Task.CompletedTask;
    }

    public Task ClearConfiguredPrinterAsync()
    {
        Preferences.Default.Remove(PrinterSettingsKey);
        return Task.CompletedTask;
    }
}
