namespace TolllgaFinale.Services;

/// <summary>Stocke l'imprimante par défaut choisie par l'utilisateur (persisté sur l'appareil).</summary>
public class PrinterSettingsService
{
    private const string Key = "DefaultPrinterName";

    public string? GetDefaultPrinter()
    {
        var value = Preferences.Default.Get(Key, string.Empty);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void SetDefaultPrinter(string? printerName)
    {
        Preferences.Default.Set(Key, printerName ?? string.Empty);
    }
}