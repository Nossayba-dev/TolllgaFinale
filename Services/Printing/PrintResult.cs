namespace TolllgaFinale.Services;

/// <summary>
/// Represents the outcome of a print operation.
/// </summary>
public sealed class PrintResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? PdfPath { get; init; }
    public string? PrinterName { get; init; }

    public static PrintResult Ok(string message, string? pdfPath = null, string? printerName = null)
        => new() { Success = true, Message = message, PdfPath = pdfPath, PrinterName = printerName };

    public static PrintResult Fail(string message)
        => new() { Success = false, Message = message };
}
