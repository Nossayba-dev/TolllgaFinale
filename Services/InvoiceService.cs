using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TolllgaFinale.Models;

namespace TolllgaFinale.Services
{
    /// <summary>
    /// Service responsible for generating PDF tickets (invoices) from a WeightRecord.
    /// - Keeps PDF creation independent from UI and printing logic.
    /// - Saves PDFs in a local "Tickets" folder inside the app data directory.
    /// </summary>
    public interface IInvoiceService
    {
        /// <summary>
        /// Generates a PDF for the provided weight record and returns the full file path.
        /// </summary>
        Task<string> GeneratePdfAsync(WeightRecord record);
    }

    public class InvoiceService : IInvoiceService
    {
        private const string TicketsFolderName = "Tickets";
        private const string CompanyNamePlaceholder = "COMPANY NAME"; // keep as placeholder

        public InvoiceService()
        {
        }

        /// <summary>
        /// Generates the PDF on a background thread and returns the saved path.
        /// </summary>
        public async Task<string> GeneratePdfAsync(WeightRecord record)
        {
            if (record is null) throw new ArgumentNullException(nameof(record));

            // Ensure tickets directory exists
            var ticketsDir = Path.Combine(FileSystem.AppDataDirectory, TicketsFolderName);
            Directory.CreateDirectory(ticketsDir);

            // Use record id and timestamp to create a unique filename
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var fileName = $"ticket_{record.Id}_{timestamp}.pdf";
            var filePath = Path.Combine(ticketsDir, fileName);

            // Generate PDF off the UI thread
            await Task.Run(() =>
            {
                var doc = new TicketDocument(record);
                // The GeneratePdf call writes the output file synchronously
                doc.GeneratePdf(filePath);
            });

            return filePath;
        }

        /// <summary>
        /// Internal QuestPDF document implementation. Kept private so InvoiceService controls creation.
        /// The document is organized into reusable sections for maintainability.
        /// </summary>
        private class TicketDocument : IDocument
        {
            private readonly WeightRecord _r;

            public TicketDocument(WeightRecord record)
            {
                _r = record;
            }

            public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

            public void Compose(IDocumentContainer container)
            {
                container.Page(page =>
                {
                    // A4 by default, portrait. Margins chosen for printability.
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Element(header => ComposeHeader(header));
                    page.Content().Element(content => ComposeContent(content));
                    page.Footer().Element(footer => ComposeFooter(footer));
                });
            }

            // Header section with logo placeholder, company name and title.
            // QuestPDF containers are single-child containers, so the original code was invalid:
            // it assigned a Row and then a separator line directly to the same IContainer.
            // Wrapping both fragments in a Column keeps one direct child per container and avoids
            // DocumentComposeException.
            void ComposeHeader(QuestPDF.Infrastructure.IContainer container)
            {
                container.Column(col =>
                {
                    col.Item().PaddingBottom(10).Row(row =>
                    {
                        row.ConstantItem(80).Height(60).AlignMiddle().AlignLeft().Element(logo =>
                        {
                            // Simple placeholder box for logo
                            logo.Border(1).Padding(6).AlignCenter().AlignMiddle().Column(logoCol =>
                            {
                                logoCol.Item().Text("Logo").FontSize(10).SemiBold();
                            });
                        });

                        row.RelativeItem().PaddingLeft(10).Column(textCol =>
                        {
                            textCol.Item().Text(CompanyNamePlaceholder).FontSize(16).SemiBold();
                            textCol.Item().Text("WEIGHING TICKET").FontSize(14).Bold().FontColor(QuestPDF.Helpers.Colors.Black);
                        });

                        row.ConstantItem(120).AlignRight().Column(infoCol =>
                        {
                            infoCol.Item().Text($"Ticket #: {_r.Id}").SemiBold().AlignRight();
                            infoCol.Item().Text($"Matricule: {(_r.Matricule ?? "—")}").AlignRight();
                        });
                    });

                    // Separator line belongs to the same parent Column as the header row.
                    col.Item().Height(1).Background(QuestPDF.Helpers.Colors.Grey.Lighten2);
                });
            }

            // Main content composes truck info, weights, financial, dates, operators and observation
            void ComposeContent(QuestPDF.Infrastructure.IContainer container)
            {
                container.PaddingTop(8).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Element(ComposeTruckInfo);

                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Element(ComposeWeights);
                        r.ConstantItem(16);
                        r.RelativeItem().Element(ComposeFinancial);
                    });

                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Element(ComposeDates);
                        r.ConstantItem(16);
                        r.RelativeItem().Element(ComposeOperators);
                    });

                    col.Item().Element(ComposeObservation);
                });
            }

            void ComposeFooter(QuestPDF.Infrastructure.IContainer container)
            {
                container.PaddingTop(12).Column(col =>
                {
                    col.Item().AlignCenter().Text("Thank you").SemiBold();

                    col.Item().PaddingTop(18).Row(r =>
                    {
                        r.RelativeItem().Column(sig =>
                        {
                            sig.Item().Text("Signature:").FontSize(10);
                            sig.Item().PaddingTop(24).Container().Height(40).Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2);
                        });

                        r.ConstantItem(12);

                        r.RelativeItem().Column(notice =>
                        {
                            notice.Item().Text("Generated by: " + CompanyNamePlaceholder).FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                            notice.Item().Text($"Date: {FormatDate(_r.GetLatestWeighingDate())}").FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        });
                    });
                });
            }

            // Truck information block
            void ComposeTruckInfo(QuestPDF.Infrastructure.IContainer container)
            {
                container.Padding(6).Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Column(col =>
                {
                    col.Item().Text("Truck Information").Bold();
                    col.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Matricule: {(_r.Matricule ?? "—")}");
                            c.Item().Text($"Driver: {(_r.DriverName ?? "—")}");
                        });

                        r.ConstantItem(12);

                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Product: {(_r.Product ?? "—")}");
                            c.Item().Text($"Ticket #: {_r.Id}");
                        });
                    });
                });
            }

            // Weights block (simplified table using rows)
            void ComposeWeights(QuestPDF.Infrastructure.IContainer container)
            {
                container.Padding(6).Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Column(col =>
                {
                    col.Item().Text("Weights").Bold();
                    col.Item().PaddingTop(6).Column(c =>
                    {
                        c.Spacing(6);

                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Gross Weight (kg)");
                            r.ConstantItem(120).AlignRight().Text(FormatNumber(_r.GrossWeight));
                        });

                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Tare (kg)");
                            r.ConstantItem(120).AlignRight().Text(FormatNumber(_r.Tare));
                        });

                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Net Weight (kg)");
                            r.ConstantItem(120).AlignRight().Text(FormatNumber(_r.NetWeight)).FontColor(QuestPDF.Helpers.Colors.Green.Darken1);
                        });
                    });
                });
            }

            // Financial block (amount)
            void ComposeFinancial(QuestPDF.Infrastructure.IContainer container)
            {
                container.Padding(6).Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Column(col =>
                {
                    col.Item().Text("Financial").Bold();
                    col.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Amount").FontSize(12);
                            c.Item().Text(FormatNumber(_r.Amount)).FontSize(12).SemiBold().AlignRight();
                        });
                    });
                });
            }

            // Dates block
            void ComposeDates(QuestPDF.Infrastructure.IContainer container)
            {
                container.Padding(6).Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Column(col =>
                {
                    col.Item().Text("Dates").Bold();
                    col.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Tare Date").FontSize(10).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                            c.Item().Text(FormatDate(_r.WeighingDateTare));
                        });

                        r.ConstantItem(12);

                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Gross Date").FontSize(10).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                            c.Item().Text(FormatDate(_r.WeighingDateGross));
                        });
                    });
                });
            }

            // Operators block
            void ComposeOperators(QuestPDF.Infrastructure.IContainer container)
            {
                container.Padding(6).Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Column(col =>
                {
                    col.Item().Text("Operators").Bold();
                    col.Item().PaddingTop(6).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Operator Tare").FontSize(10).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                            c.Item().Text(FormatOperator(_r.OperatorTare));
                        });

                        r.ConstantItem(12);

                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Operator Gross").FontSize(10).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                            c.Item().Text(FormatOperator(_r.OperatorGross));
                        });
                    });
                });
            }

            // Observation block
            void ComposeObservation(QuestPDF.Infrastructure.IContainer container)
            {
                container.Padding(6).Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Column(col =>
                {
                    col.Item().Text("Observation").Bold();
                    col.Item().PaddingTop(6).Text(string.IsNullOrWhiteSpace(_r.Observation) ? "No observation" : _r.Observation!);
                });
            }

            // Helpers
            static string FormatDate(DateTime value)
                => value == default ? "—" : value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

            static string FormatNumber(double value)
                => value == 0 ? "—" : value.ToString("N3");

            static string FormatOperator(string? value)
                => string.IsNullOrWhiteSpace(value) ? "—" : value!;
        }
    }
}
