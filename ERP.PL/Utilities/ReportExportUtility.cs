using System.Text;
namespace ERP.PL.Utilities
{
    public static class ReportExportUtility
    {
        public static byte[] ToCsv(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(',', headers.Select(EscapeCsv)));

            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(',', row.Select(v => EscapeCsv(v ?? string.Empty))));
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public static byte[] ToExcelTsv(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join('\t', headers));
            foreach (var row in rows)
            {
                sb.AppendLine(string.Join('\t', row.Select(v => v?.Replace('\t', ' ') ?? string.Empty)));
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public static byte[] ToPdfText(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
        {
            var content = new StringBuilder();
            content.AppendLine(title);
            content.AppendLine(new string('-', Math.Max(20, title.Length)));
            content.AppendLine(string.Join(" | ", headers));
            content.AppendLine(new string('-', 100));
            foreach (var row in rows.Take(200))
            {
                content.AppendLine(string.Join(" | ", row.Select(v => (v ?? string.Empty).Replace("|", "/"))));
            }

            return BuildMinimalPdf(content.ToString());
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";

            return value;
        }

        private static byte[] BuildMinimalPdf(string text)
        {
            var safeText = text
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("\r", string.Empty)
                .Replace("\n", "\\n");

            var stream = $"BT /F1 10 Tf 40 800 Td ({safeText}) Tj ET";
            var pdf = $"%PDF-1.4\n" +
                      "1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n" +
                      "2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj\n" +
                      "3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>endobj\n" +
                      $"4 0 obj<< /Length {stream.Length} >>stream\n{stream}\nendstream endobj\n" +
                      "5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n" +
                      "xref\n0 6\n0000000000 65535 f \n" +
                      "0000000010 00000 n \n0000000060 00000 n \n0000000117 00000 n \n0000000243 00000 n \n0000000000 00000 n \n" +
                      "trailer<< /Root 1 0 R /Size 6 >>\nstartxref\n0\n%%EOF";

            return Encoding.ASCII.GetBytes(pdf);
        }
    }
}
