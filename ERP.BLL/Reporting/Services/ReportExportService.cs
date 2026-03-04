using ERP.BLL.Reporting.Dtos;
using ERP.BLL.Reporting.Interfaces;

namespace ERP.BLL.Reporting.Services;

/// <summary>
/// Centralized export service that delegates to <see cref="ReportFileBuilder"/>
/// and ensures consistent content types, file extensions, and formatting
/// across all report types and export formats.
/// </summary>
public sealed class ReportExportService : IReportExportService
{
    public ExportResult Export(
        string title,
        string fileNameBase,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        ExportFormat format,
        string? filterSummary = null)
    {
        return format switch
        {
            ExportFormat.Pdf => new ExportResult
            {
                FileBytes = ReportFileBuilder.ToPdf(title, headers, rows, filterSummary),
                ContentType = "application/pdf",
                FileName = $"{fileNameBase}.pdf"
            },
            ExportFormat.Html => new ExportResult
            {
                FileBytes = ReportFileBuilder.ToStyledHtml(title, headers, rows, filterSummary),
                ContentType = "text/html; charset=utf-8",
                FileName = $"{fileNameBase}.html"
            },
            ExportFormat.Csv => new ExportResult
            {
                FileBytes = ReportFileBuilder.ToCsv(title, headers, rows),
                ContentType = "text/csv; charset=utf-8",
                FileName = $"{fileNameBase}.csv"
            },
            ExportFormat.Excel => new ExportResult
            {
                FileBytes = ReportFileBuilder.ToExcelHtml(title, headers, rows),
                // Excel HTML uses the older .xls MIME type; Excel will open the HTML file correctly.
                ContentType = "application/vnd.ms-excel",
                FileName = $"{fileNameBase}.xls"
            },
            _ => new ExportResult
            {
                FileBytes = ReportFileBuilder.ToPdf(title, headers, rows, filterSummary),
                ContentType = "application/pdf",
                FileName = $"{fileNameBase}.pdf"
            }
        };
    }
}
