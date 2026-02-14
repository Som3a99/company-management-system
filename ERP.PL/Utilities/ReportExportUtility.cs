using ERP.BLL.Reporting.Services;
namespace ERP.PL.Utilities
{

    public static class ReportExportUtility
    {
        public static byte[] ToCsv(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
            => ReportFileBuilder.ToCsv(title, headers, rows);

        public static byte[] ToExcel(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows, string title)
            => ReportFileBuilder.ToExcelHtml(title, headers, rows);

        public static byte[] ToPdf(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
            => ReportFileBuilder.ToSimplePdf(title, headers, rows);
    }
}
