using System.Drawing.Printing;
using TolllgaFinale.Models;
using TolllgaFinale.Services;


using SDFont = System.Drawing.Font;
using SDFontStyle = System.Drawing.FontStyle;
using SDBrushes = System.Drawing.Brushes;
using SDPens = System.Drawing.Pens;

namespace TolllgaFinale.Services;

public class WindowsPrintService : IPrintService
{
    public List<string> GetInstalledPrinters()
    {
        var list = new List<string>();
        foreach (string name in PrinterSettings.InstalledPrinters)
            list.Add(name);
        return list;
    }

    public Task<bool> PrintTicketAsync(TicketData ticket, string? printerName = null, int copies = 1)
    {
        var doc = new PrintDocument
        {
            DocumentName = $"Ticket_{ticket.Matricule}_{ticket.RecordId}"
        };

        if (!string.IsNullOrWhiteSpace(printerName))
            doc.PrinterSettings.PrinterName = printerName;

        if (!doc.PrinterSettings.IsValid)
            return Task.FromResult(false);

        doc.PrinterSettings.Copies = (short)Math.Max(1, copies);

        doc.PrintPage += (s, e) => DrawTicket(e, ticket);

        try
        {
            doc.Print();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static void DrawTicket(PrintPageEventArgs e, TicketData t)
    {
        var g = e.Graphics!;
        var titleFont = new SDFont("Segoe UI", 16, SDFontStyle.Bold);
        var labelFont = new SDFont("Segoe UI", 10, SDFontStyle.Bold);
        var valueFont = new SDFont("Segoe UI", 10, SDFontStyle.Regular);
        float x = 40, y = 40, lineHeight = 24;

        g.DrawString("TICKET DE PESÉE", titleFont, SDBrushes.Black, x, y);
        y += 40;
        g.DrawLine(SDPens.Black, x, y, e.PageBounds.Width - 40, y);
        y += 20;

        void Line(string label, string value)
        {
            g.DrawString(label, labelFont, SDBrushes.Black, x, y);
            g.DrawString(value, valueFont, SDBrushes.Black, x + 180, y);
            y += lineHeight;
        }

        Line("N° Ticket :", t.RecordId.ToString());
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
        Line("Opérateur :", t.OperatorGross ?? t.OperatorTare ?? "-");

        y += 30;
        g.DrawLine(SDPens.Black, x, y, e.PageBounds.Width - 40, y);
        y += 20;
        g.DrawString("Signature : ______________________", valueFont, SDBrushes.Black, x, y);
    }
}