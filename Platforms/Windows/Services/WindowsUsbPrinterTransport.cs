using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TolllgaFinale.Services;

/// <summary>
/// Windows implementation of the low-level print transport.
/// It sends the generated PDF to the configured Windows printer using available PDF print handlers.
/// </summary>
public sealed class WindowsUsbPrinterTransport : IPrinterTransport
{
    public async Task<PrintResult> SendAsync(byte[] data, PrinterConfiguration printer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(printer);

        var printerName = !string.IsNullOrWhiteSpace(printer.DeviceName)
            ? printer.DeviceName
            : printer.DisplayName;

        if (string.IsNullOrWhiteSpace(printerName))
            return PrintResult.Fail("Aucun nom d'imprimante valide n'a été configuré.");

        var tempPdfPath = Path.Combine(Path.GetTempPath(), $"tolllga_{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(tempPdfPath, data, cancellationToken);

        string? originalDefaultPrinter = null;
        try
        {
            originalDefaultPrinter = GetDefaultPrinterName();
            SetDefaultPrinterIfPossible(printerName);

            if (await TryPrintWithShellVerbAsync(tempPdfPath, printerName, cancellationToken))
                return PrintResult.Ok("Document envoyé à l'imprimante Windows.", printerName: printerName);

            if (await TryPrintWithDedicatedPdfAppAsync(tempPdfPath, printerName, cancellationToken))
                return PrintResult.Ok("Document envoyé à l'imprimante Windows.", printerName: printerName);

            return PrintResult.Fail($"Aucune application PDF compatible n'a été trouvée pour imprimer '{printerName}'.");
        }
        catch (OperationCanceledException)
        {
            return PrintResult.Fail("Impression annulée.");
        }
        catch (Exception ex)
        {
            return PrintResult.Fail($"Échec de l'impression Windows pour '{printerName}': {ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(originalDefaultPrinter))
                SetDefaultPrinterIfPossible(originalDefaultPrinter);

            try
            {
                if (File.Exists(tempPdfPath))
                    File.Delete(tempPdfPath);
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }

    private static async Task<bool> TryPrintWithShellVerbAsync(string pdfPath, string printerName, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pdfPath,
                Arguments = $"\"{printerName}\"",
                Verb = "printto",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return false;

            await Task.Run(() => process.WaitForExit(15000), cancellationToken);
            return process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryPrintWithDedicatedPdfAppAsync(string pdfPath, string printerName, CancellationToken cancellationToken)
    {
        foreach (var candidate in GetPdfAppCandidates())
        {
            if (!File.Exists(candidate))
                continue;

            try
            {
                var arguments = GetPrintArguments(candidate, pdfPath);
                if (arguments is null)
                    continue;

                var startInfo = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                    continue;

                await Task.Run(() => process.WaitForExit(15000), cancellationToken);
                return true;
            }
            catch
            {
                // Try next candidate.
            }
        }

        return false;
    }

    private static IEnumerable<string> GetPdfAppCandidates()
    {
        var candidates = new List<string>();
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (!string.IsNullOrWhiteSpace(pf))
        {
            candidates.Add(Path.Combine(pf, "Microsoft", "Edge", "Application", "msedge.exe"));
            candidates.Add(Path.Combine(pf, "Adobe", "Acrobat Reader DC", "Reader", "AcroRd32.exe"));
            candidates.Add(Path.Combine(pf, "Adobe", "Acrobat DC", "Acrobat", "Acrobat.exe"));
            candidates.Add(Path.Combine(pf, "SumatraPDF", "SumatraPDF.exe"));
        }

        if (!string.IsNullOrWhiteSpace(pf86))
        {
            candidates.Add(Path.Combine(pf86, "Microsoft", "Edge", "Application", "msedge.exe"));
            candidates.Add(Path.Combine(pf86, "Adobe", "Acrobat Reader DC", "Reader", "AcroRd32.exe"));
            candidates.Add(Path.Combine(pf86, "Adobe", "Acrobat DC", "Acrobat", "Acrobat.exe"));
            candidates.Add(Path.Combine(pf86, "SumatraPDF", "SumatraPDF.exe"));
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetPrintArguments(string executable, string pdfPath)
    {
        var name = Path.GetFileName(executable);

        if (name.Equals("SumatraPDF.exe", StringComparison.OrdinalIgnoreCase))
            return $"-print-to-default -silent \"{pdfPath}\"";

        if (name.Equals("AcroRd32.exe", StringComparison.OrdinalIgnoreCase) || name.Equals("Acrobat.exe", StringComparison.OrdinalIgnoreCase))
            return $"/t \"{pdfPath}\"";

        if (name.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase))
            return $"--kiosk-printing --new-window \"file:///{pdfPath.Replace('\\', '/')}\"";

        return null;
    }

    private static string? GetDefaultPrinterName()
    {
        var capacity = 256;
        var sb = new StringBuilder(capacity);
        return GetDefaultPrinter(sb, ref capacity) ? sb.ToString() : null;
    }

    private static void SetDefaultPrinterIfPossible(string printerName)
    {
        try
        {
            SetDefaultPrinter(printerName);
        }
        catch
        {
            // Ignore if the OS blocks changing the default printer.
        }
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetDefaultPrinter(StringBuilder pszBuffer, ref int pcchBuffer);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDefaultPrinter(string pszPrinter);
}
