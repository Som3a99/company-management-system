using ERP.BLL.Reporting.Services;

namespace ERP.PL.Utilities
{
    /// <summary>
    /// Thin backward-compatible wrapper over <see cref="ReportFileBuilder"/>.
    /// New code should use <see cref="ERP.BLL.Reporting.Interfaces.IReportExportService"/> instead.
    /// </summary>
    public static class ReportExportUtility
    {
        public static byte[] ToCsv(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
            => ReportFileBuilder.ToCsv(title, headers, rows);

        public static byte[] ToHtml(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
            => ReportFileBuilder.ToStyledHtml(title, headers, rows);

        public static byte[] ToExcel(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows, string title)
            => ReportFileBuilder.ToExcelHtml(title, headers, rows);

        public static byte[] ToPdf(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
            => ReportFileBuilder.ToPdf(title, headers, rows);
    }
}
