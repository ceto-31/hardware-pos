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

    public static void Export(string filePath, ReportDocument document)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var generatedAt = DateTime.Now;
        var columnCount = document.Headers.Count;
        var rightAligned = document.RightAlignedColumns.ToHashSet();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Segoe UI"));

                page.Header().Column(column =>
                {
                    column.Item().Text("4KV Hardware").Bold().FontSize(18).FontColor(PrimaryBlue);
                    column.Item().PaddingTop(4).Text(document.Title).SemiBold().FontSize(14);
                    column.Item().PaddingTop(8).Column(meta =>
                    {
                        if (document.HeaderFields.Count > 0)
                        {
                            foreach (var field in document.HeaderFields)
                            {
                                meta.Item().Text(text =>
                                {
                                    text.Span($"{field.Label}: ").FontColor(MutedText);
                                    text.Span(field.Value);
                                });
                            }
                        }
                        else
                        {
                            meta.Item().Text(text =>
                            {
                                text.Span($"{document.PeriodLabel}: ").FontColor(MutedText);
                                text.Span(document.PeriodValue);
                            });
                        }
                        meta.Item().Text(text =>
                        {
                            text.Span("Generated: ").FontColor(MutedText);
                            text.Span(generatedAt.ToString("MMMM dd, yyyy hh:mm tt", SalesReportText.Culture));
                        });
                        meta.Item().Text(text =>
                        {
                            text.Span("Records: ").FontColor(MutedText);
                            text.Span(document.Rows.Count.ToString(SalesReportText.Culture));
                        });
                    });
                    column.Item().PaddingTop(12).LineHorizontal(1).LineColor("#E2E8F0");
                });

                page.Content().PaddingTop(16).Column(column =>
                {
                    foreach (var line in document.SummaryLines)
                        column.Item().Text(line).SemiBold().FontSize(11);

                    if (document.SummaryLines.Count > 0)
                        column.Item().PaddingBottom(8);

                    foreach (var note in document.Notes)
                        column.Item().PaddingBottom(2).Text(note).FontSize(9).FontColor(MutedText);

                    if (document.Notes.Count > 0)
                        column.Item().PaddingBottom(8);

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            if(columnCount==5)
                            {
                                columns.ConstantColumn(128); columns.RelativeColumn();
                                columns.ConstantColumn(76); columns.ConstantColumn(55); columns.ConstantColumn(88);
                            }
                            else
                                for (var i = 0; i < columnCount; i++)
                                    columns.RelativeColumn(i==0 && columnCount==4 ? 2f : 1.1f);
                        });

                        table.Header(header =>
                        {
                            for (var i = 0; i < columnCount; i++)
                            {
                                var caption = document.Headers[i];
                                var alignRight = rightAligned.Contains(i);
                                header.Cell().Background(PrimaryBlue).Padding(8).Element(cell =>
                                {
                                    var text = cell.Text(caption).FontColor(Colors.White).SemiBold();
                                    if (alignRight)
                                        text.AlignRight();
                                });
                            }
                        });

                        for (var rowIndex = 0; rowIndex < document.Rows.Count; rowIndex++)
                            WriteRow(table, document.Rows[rowIndex], columnCount, rightAligned, rowIndex % 2 == 1, bold: false);

                        if (document.TotalRow is { Length: > 0 } total)
                            WriteRow(table, total, columnCount, rightAligned, zebra: false, bold: true);
                    });
                    foreach (var section in document.Sections)
                    {
                        column.Item().PaddingTop(18).Text(section.Title).SemiBold().FontSize(13);
                        if(section.Rows.Count==0)
                        {
                            column.Item().Text("No products recorded in this period.").FontColor(MutedText);
                            continue;
                        }
                        column.Item().PaddingTop(8).Table(table=>
                        {
                            var numeric=section.RightAlignedColumns.ToHashSet();
                            table.ColumnsDefinition(columns=>
                            {
                                for(var i=0;i<section.Headers.Count;i++) columns.RelativeColumn(i==0?2.5f:1f);
                            });
                            table.Header(header=>
                            {
                                for(var i=0;i<section.Headers.Count;i++)
                                {
                                    var cell=header.Cell().Background(PrimaryBlue).Padding(6);
                                    if(numeric.Contains(i)) cell=cell.AlignRight();
                                    cell.Text(section.Headers[i]).FontColor(Colors.White).SemiBold();
                                }
                            });
                            for(var i=0;i<section.Rows.Count;i++) WriteRow(table,section.Rows[i],section.Headers.Count,numeric,i%2==1,false);
                        });
                    }
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

    private static void WriteRow(
        TableDescriptor table,
        string[] values,
        int columnCount,
        HashSet<int> rightAligned,
        bool zebra,
        bool bold)
    {
        for (var i = 0; i < columnCount; i++)
        {
            var value = i < values.Length ? values[i] : string.Empty;
            var alignRight = rightAligned.Contains(i);
            table.Cell().Element(cell =>
            {
                var styled = cell
                    .Background(bold ? "#EFF6FF" : zebra ? ZebraFill : Colors.White)
                    .BorderBottom(1)
                    .BorderColor("#E2E8F0")
                    .Padding(6);
                if (alignRight)
                    styled = styled.AlignRight();
                var text = styled.Text(value);
                if (bold)
                    text.SemiBold();
            });
        }
    }
}
