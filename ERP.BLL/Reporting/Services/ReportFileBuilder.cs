using System.Text;

namespace ERP.BLL.Reporting.Services
{
    public static class ReportFileBuilder
    {
        public static byte[] ToCsv(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Report,{EscapeCsv(title)}");
            sb.AppendLine($"Generated At (UTC),{EscapeCsv(DateTime.UtcNow.ToString("u"))}");
            sb.AppendLine();
            sb.AppendLine(string.Join(',', headers.Select(EscapeCsv)));

            foreach (var row in rows)
                sb.AppendLine(string.Join(',', row.Select(v => EscapeCsv(v ?? string.Empty))));

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        public static byte[] ToExcelHtml(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<html><head><meta charset='utf-8'></head><body style='font-family:Segoe UI,Arial,sans-serif;'>");
            sb.Append($"<h2>{System.Net.WebUtility.HtmlEncode(title)}</h2>");
            sb.Append($"<p><strong>Generated At (UTC):</strong> {DateTime.UtcNow:u}</p>");
            sb.Append("<table border='1' cellspacing='0' cellpadding='6' style='border-collapse:collapse;'>");
            sb.Append("<thead style='background:#f5f5f5;'><tr>");
            foreach (var h in headers) sb.Append($"<th>{System.Net.WebUtility.HtmlEncode(h)}</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var row in rows)
            {
                sb.Append("<tr>");
                foreach (var c in row)
                    sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(c ?? string.Empty)}</td>");
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table></body></html>");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public static byte[] ToSimplePdf(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
        {
            var lines = new List<string>
        {
            title,
            $"Generated At (UTC): {DateTime.UtcNow:u}",
            string.Empty,
            string.Join(" | ", headers),
            new string('-', 130)
        };

            foreach (var row in rows.Take(400))
            {
                lines.Add(string.Join(" | ", row.Select(v => (v ?? string.Empty).Replace("|", "/"))));
            }

            return BuildPaginatedTextPdf(lines);
        }

        private static byte[] BuildPaginatedTextPdf(IReadOnlyList<string> rawLines)
        {
            const int maxCharsPerLine = 100;
            const int linesPerPage = 46;

            var wrapped = new List<string>();
            foreach (var line in rawLines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    wrapped.Add(string.Empty);
                    continue;
                }

                var remaining = line;
                while (remaining.Length > maxCharsPerLine)
                {
                    var breakAt = remaining.LastIndexOf(' ', maxCharsPerLine);
                    if (breakAt <= 0) breakAt = maxCharsPerLine;
                    wrapped.Add(remaining[..breakAt]);
                    remaining = remaining[breakAt..].TrimStart();
                }

                wrapped.Add(remaining);
            }

            var pages = wrapped
                .Select((line, idx) => new { line, idx })
                .GroupBy(x => x.idx / linesPerPage)
                .Select(g => g.Select(x => x.line).ToList())
                .ToList();

            if (pages.Count == 0)
                pages.Add(new List<string> { "No data." });

            var objects = new List<string>();
            var pageObjectIds = new List<int>();
            var fontObjectId = 3 + (pages.Count * 2);

            objects.Add("1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n");

            for (var i = 0; i < pages.Count; i++)
            {
                var pageObjectId = 3 + (i * 2);
                var contentObjectId = pageObjectId + 1;
                pageObjectIds.Add(pageObjectId);

                var stream = BuildPageStream(pages[i]);
                objects.Add($"{pageObjectId} 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents {contentObjectId} 0 R /Resources << /Font << /F1 {fontObjectId} 0 R >> >> >>endobj\n");
                objects.Add($"{contentObjectId} 0 obj<< /Length {Encoding.ASCII.GetByteCount(stream)} >>stream\n{stream}\nendstream endobj\n");
            }

            var kids = string.Join(' ', pageObjectIds.Select(id => $"{id} 0 R"));
            objects.Insert(1, $"2 0 obj<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>endobj\n");

            objects.Add($"{fontObjectId} 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>endobj\n");

            return SerializePdf(objects);
        }

        private static string BuildPageStream(IReadOnlyList<string> pageLines)
        {
            var sb = new StringBuilder();
            sb.AppendLine("BT");
            sb.AppendLine("/F1 9 Tf");
            sb.AppendLine("14 TL");
            sb.AppendLine("36 805 Td");

            foreach (var line in pageLines)
            {
                sb.Append('(').Append(EscapePdf(line)).AppendLine(") Tj");
                sb.AppendLine("T*");
            }

            sb.Append("ET");
            return sb.ToString();
        }

        private static byte[] SerializePdf(IReadOnlyList<string> objects)
        {
            var body = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int> { 0 };

            foreach (var obj in objects)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(body.ToString()));
                body.Append(obj);
            }

            var xrefPos = Encoding.ASCII.GetByteCount(body.ToString());
            body.Append($"xref\n0 {objects.Count + 1}\n");
            body.Append("0000000000 65535 f \n");
            for (var i = 1; i <= objects.Count; i++)
                body.Append($"{offsets[i]:D10} 00000 n \n");

            body.Append($"trailer<< /Root 1 0 R /Size {objects.Count + 1} >>\nstartxref\n{xrefPos}\n%%EOF");

            return Encoding.ASCII.GetBytes(body.ToString());
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        private static string EscapePdf(string value)
            => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
