using Android.Content;
using Android.OS;
using Android.Print;
using Android.Print.Pdf;
using Microsoft.Win32.SafeHandles;
using TolllgaFinale.Models;

// Alias pour éviter l'ambiguïté avec Microsoft.Maui.Graphics.Paint
using AndroidPaint = Android.Graphics.Paint;
using AndroidCanvas = Android.Graphics.Canvas;
using AndroidColor = Android.Graphics.Color;

namespace TolllgaFinale.Services;

public class AndroidStandardPrintService : IPrintService
{
    public List<string> GetInstalledPrinters()
    {
        // Android n'autorise pas les apps à lister les imprimantes installées (confidentialité).
        // Le sélecteur natif s'affiche automatiquement à chaque impression.
        return new List<string>();
    }

    public Task<bool> PrintTicketAsync(TicketData ticket, string? printerName = null)
    {
        var context = Platform.CurrentActivity ?? Platform.AppContext;
        var printManager = (PrintManager)context.GetSystemService(Context.PrintService)!;

        var jobName = $"Ticket_{ticket.Matricule}_{ticket.RecordId}";
        var adapter = new TicketPrintDocumentAdapter(ticket);

        printManager.Print(jobName, adapter, null);

        // Android gère le job d'impression de façon asynchrone via son propre écran ;
        // on ne peut pas récupérer un succès/échec fiable ici comme sur Windows.
        return Task.FromResult(true);
    }
}

internal class TicketPrintDocumentAdapter : PrintDocumentAdapter
{
    private readonly TicketData _ticket;
    private PrintedPdfDocument? _pdfDocument;

    public TicketPrintDocumentAdapter(TicketData ticket) => _ticket = ticket;

    public override void OnLayout(PrintAttributes? oldAttributes, PrintAttributes newAttributes,
        CancellationSignal? cancellationSignal, LayoutResultCallback callback, Bundle? extras)
    {
        _pdfDocument = new PrintedPdfDocument(Platform.AppContext, newAttributes);

        if (cancellationSignal?.IsCanceled == true)
        {
            callback.OnLayoutCancelled();
            return;
        }

        var info = new PrintDocumentInfo.Builder($"Ticket_{_ticket.RecordId}.pdf")
            .SetContentType(PrintContentType.Document)
            .SetPageCount(1)
            .Build();

        callback.OnLayoutFinished(info, true);
    }

    public override void OnWrite(PageRange[]? pages, ParcelFileDescriptor destination,
        CancellationSignal? cancellationSignal, WriteResultCallback callback)
    {
        var page = _pdfDocument!.StartPage(0);
        DrawTicket(page.Canvas!, _ticket);
        _pdfDocument.FinishPage(page);

        try
        {
            using var handle = new SafeFileHandle(new IntPtr(destination.DetachFd()), true);
            using var stream = new FileStream(handle, FileAccess.Write);
            _pdfDocument.WriteTo(stream);
        }
        catch
        {
            callback.OnWriteFailed("Erreur d'écriture PDF");
            return;
        }
        finally
        {
            _pdfDocument.Close();
        }

        callback.OnWriteFinished(new[] { PageRange.AllPages! });
    }

    private static void DrawTicket(AndroidCanvas canvas, TicketData t)
    {
        var titlePaint = new AndroidPaint { TextSize = 28, FakeBoldText = true, AntiAlias = true };
        var labelPaint = new AndroidPaint { TextSize = 14, FakeBoldText = true, AntiAlias = true };
        var valuePaint = new AndroidPaint { TextSize = 14, AntiAlias = true };
        var linePaint = new AndroidPaint { StrokeWidth = 1, Color = AndroidColor.Black };

        float x = 40, y = 60, lineHeight = 26;

        canvas.DrawText("TICKET DE PESEE", x, y, titlePaint);
        y += 30;
        canvas.DrawLine(x, y, canvas.Width - 40, y, linePaint);
        y += 20;

        void Line(string label, string value)
        {
            canvas.DrawText(label, x, y, labelPaint);
            canvas.DrawText(value, x + 180, y, valuePaint);
            y += lineHeight;
        }

        Line("N Ticket :", t.RecordId.ToString());
        Line("Matricule :", t.Matricule);
        Line("Chauffeur :", t.DriverName ?? "-");
        Line("Produit :", t.Product ?? "-");
        Line("Observation :", t.Observation ?? "-");
        y += 10;
        Line("Poids Brut :", $"{t.GrossWeight:0.000} kg");
        Line("Tare :", $"{t.Tare:0.000} kg");
        Line("Poids Net :", $"{t.NetWeight:0.000} kg");
        y += 10;
        Line("Date Tare :", t.WeighingDateTare.ToString("dd/MM/yyyy HH:mm"));
        Line("Date Brut :", t.WeighingDateGross.ToString("dd/MM/yyyy HH:mm"));
        Line("Operateur :", t.OperatorGross ?? t.OperatorTare ?? "-");

        y += 30;
        canvas.DrawLine(x, y, canvas.Width - 40, y, linePaint);
        y += 20;
        canvas.DrawText("Signature : ______________________", x, y, valuePaint);
    }
}