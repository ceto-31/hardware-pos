using HardwarePOS.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HardwarePOS.Services;

public static class ReportPdfExporter
{
    private const string PrimaryBlue = "#2563EB";
    private const string MutedText = "#64748B";
    private const string ZebraFill = "#F8FAFC";

    public static void Export(
        string filePath,
        string reportType,
        int? year,
        IReadOnlyList<string> headers,
        IReadOnlyList<ReportRow> rows,
        string? summaryLine = null)
    {
        var generatedAt = DateTime.Now;
        var columnCount = headers.Count;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text("4KV Hardware").Bold().FontSize(18).FontColor(PrimaryBlue);
                    column.Item().PaddingTop(4).Text(reportType).SemiBold().FontSize(14);
                    column.Item().PaddingTop(8).Column(meta =>
                    {
                        meta.Item().Text(text =>
                        {
                            text.Span("Report: ").FontColor(MutedText);
                            text.Span(reportType);
                        });
                        if (year is not null)
                        {
                            meta.Item().Text(text =>
                            {
                                text.Span("Year: ").FontColor(MutedText);
                                text.Span(year.Value.ToString());
                            });
                        }
                        meta.Item().Text(text =>
                        {
                            text.Span("Generated: ").FontColor(MutedText);
                            text.Span(generatedAt.ToString("MMMM dd, yyyy hh:mm tt"));
                        });
                        meta.Item().Text(text =>
                        {
                            text.Span("Records: ").FontColor(MutedText);
                            text.Span(rows.Count.ToString());
                        });
                    });
                    column.Item().PaddingTop(12).LineHorizontal(1).LineColor("#E2E8F0");
                });

                page.Content().PaddingTop(16).Column(column =>
                {
                    if (!string.IsNullOrWhiteSpace(summaryLine))
                    {
                        column.Item().PaddingBottom(12).Text(summaryLine).SemiBold().FontSize(11);
                    }

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            for (var i = 0; i < columnCount; i++)
                                columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            foreach (var h in headers)
                            {
                                header.Cell().Background(PrimaryBlue).Padding(8)
                                    .Text(h).FontColor(Colors.White).SemiBold();
                            }
                        });

                        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                        {
                            var row = rows[rowIndex];
                            var values = GetRowValues(row, columnCount);
                            var zebra = rowIndex % 2 == 1;

                            foreach (var value in values)
                            {
                                table.Cell().Element(cell =>
                                {
                                    cell.Background(zebra ? ZebraFill : Colors.White)
                                        .BorderBottom(1)
                                        .BorderColor("#E2E8F0")
                                        .Padding(8)
                                        .Text(value);
                                });
                            }
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ").FontColor(MutedText);
                    text.CurrentPageNumber().FontColor(MutedText);
                    text.Span(" of ").FontColor(MutedText);
                    text.TotalPages().FontColor(MutedText);
                });
            });
        }).GeneratePdf(filePath);
    }

    private static IEnumerable<string> GetRowValues(ReportRow row, int columnCount)
    {
        var values = new[] { row.Col1, row.Col2, row.Col3, row.Col4 };
        for (var i = 0; i < columnCount; i++)
            yield return values[i];
    }
}
