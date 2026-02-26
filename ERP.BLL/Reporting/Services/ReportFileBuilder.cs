using System.Text;

namespace ERP.BLL.Reporting.Services
{
    public static class ReportFileBuilder
    {
        private const string CompanyName = "ERP Company Management System";
        private const string PrimaryColor = "#1a73e8";
        private const string HeaderBg = "#f8f9fa";
        private const string AltRowBg = "#f1f3f4";
        private const string BorderColor = "#dadce0";

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

        /// <summary>
        /// Generates a standalone styled HTML report file for download/viewing.
        /// </summary>
        public static byte[] ToStyledHtml(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang='en'><head><meta charset='utf-8'>");
            sb.Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.Append($"<title>{System.Net.WebUtility.HtmlEncode(title)}</title>");
            sb.Append("<style>");
            sb.Append("*, *::before, *::after { box-sizing: border-box; }");
            sb.Append("body { font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, 'Helvetica Neue', Arial, sans-serif; margin: 0; padding: 0; color: #202124; background: #fff; }");
            sb.Append($".header {{ background: linear-gradient(135deg, {PrimaryColor}, #1557b0); color: #fff; padding: 24px 32px; }}");
            sb.Append(".header h1 { margin: 0 0 4px 0; font-size: 14px; font-weight: 400; opacity: 0.85; }");
            sb.Append(".header h2 { margin: 0; font-size: 22px; font-weight: 600; }");
            sb.Append(".content { padding: 24px 32px; }");
            sb.Append(".meta { color: #5f6368; font-size: 12px; margin-bottom: 20px; }");
            sb.Append($"table {{ border-collapse: collapse; width: 100%; border: 1px solid {BorderColor}; font-size: 13px; margin-bottom: 16px; }}");
            sb.Append($"th {{ background: {HeaderBg}; color: #202124; font-weight: 600; text-align: left; padding: 10px 14px; border: 1px solid {BorderColor}; white-space: nowrap; position: sticky; top: 0; }}");
            sb.Append($"td {{ padding: 8px 14px; border: 1px solid {BorderColor}; vertical-align: top; }}");
            sb.Append($"tr:nth-child(even) td {{ background: {AltRowBg}; }}");
            sb.Append("tr:hover td { background: #e8f0fe; }");
            sb.Append($".footer {{ padding: 16px 32px; border-top: 1px solid {BorderColor}; font-size: 11px; color: #5f6368; }}");
            sb.Append("@media print { .header { -webkit-print-color-adjust: exact; print-color-adjust: exact; } }");
            sb.Append("</style></head><body>");

            sb.Append("<div class='header'>");
            sb.Append($"<h1>{System.Net.WebUtility.HtmlEncode(CompanyName)}</h1>");
            sb.Append($"<h2>{System.Net.WebUtility.HtmlEncode(title)}</h2>");
            sb.Append("</div>");

            sb.Append("<div class='content'>");
            sb.Append($"<div class='meta'>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC &bull; {rows.Count:N0} record(s)</div>");

            sb.Append("<table><thead><tr>");
            foreach (var h in headers)
                sb.Append($"<th>{System.Net.WebUtility.HtmlEncode(h)}</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var row in rows)
            {
                sb.Append("<tr>");
                foreach (var c in row)
                    sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(c ?? string.Empty)}</td>");
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
            sb.Append("</div>");
            sb.Append($"<div class='footer'>{System.Net.WebUtility.HtmlEncode(CompanyName)} &mdash; Confidential &bull; {DateTime.UtcNow:yyyy-MM-dd}</div>");
            sb.Append("</body></html>");

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public static byte[] ToExcelHtml(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.Append("<style>");
            sb.Append("body { font-family: 'Segoe UI', Calibri, Arial, sans-serif; margin: 0; padding: 20px; color: #202124; }");
            sb.Append($".header {{ background: {PrimaryColor}; color: #fff; padding: 16px 24px; margin: -20px -20px 24px -20px; }}");
            sb.Append(".header h1 { margin: 0 0 4px 0; font-size: 14px; font-weight: 400; opacity: 0.9; }");
            sb.Append(".header h2 { margin: 0; font-size: 20px; font-weight: 600; }");
            sb.Append(".meta { color: #5f6368; font-size: 12px; margin-bottom: 16px; }");
            sb.Append($"table {{ border-collapse: collapse; width: 100%; border: 1px solid {BorderColor}; font-size: 13px; }}");
            sb.Append($"th {{ background: {HeaderBg}; color: #202124; font-weight: 600; text-align: left; padding: 10px 12px; border: 1px solid {BorderColor}; white-space: nowrap; }}");
            sb.Append($"td {{ padding: 8px 12px; border: 1px solid {BorderColor}; vertical-align: top; }}");
            sb.Append($"tr:nth-child(even) td {{ background: {AltRowBg}; }}");
            sb.Append("tr:hover td { background: #e8f0fe; }");
            sb.Append($".footer {{ margin-top: 16px; padding-top: 12px; border-top: 1px solid {BorderColor}; font-size: 11px; color: #5f6368; }}");
            sb.Append("</style></head><body>");

            // Branded header
            sb.Append("<div class='header'>");
            sb.Append($"<h1>{System.Net.WebUtility.HtmlEncode(CompanyName)}</h1>");
            sb.Append($"<h2>{System.Net.WebUtility.HtmlEncode(title)}</h2>");
            sb.Append("</div>");

            sb.Append($"<div class='meta'>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC &bull; {rows.Count:N0} record(s)</div>");

            // Table
            sb.Append("<table><thead><tr>");
            foreach (var h in headers)
                sb.Append($"<th>{System.Net.WebUtility.HtmlEncode(h)}</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var row in rows)
            {
                sb.Append("<tr>");
                foreach (var c in row)
                    sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(c ?? string.Empty)}</td>");
                sb.Append("</tr>");
            }

            sb.Append("</tbody></table>");
            sb.Append($"<div class='footer'>{System.Net.WebUtility.HtmlEncode(CompanyName)} &mdash; Confidential</div>");
            sb.Append("</body></html>");

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public static byte[] ToSimplePdf(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
        {
            var colWidths = ComputeColumnWidths(headers, rows, usableWidth: 523); // 595 - 36*2 margins
            var dataRows = rows.Take(500).ToList();

            // Build table pages with structured cell layout
            var tablePages = BuildTablePages(title, headers, dataRows, colWidths, rows.Count);
            return BuildStructuredPdf(tablePages, title);
        }

        private static int[] ComputeColumnWidths(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows, int usableWidth)
        {
            var colCount = headers.Count;
            var maxLengths = new int[colCount];

            for (var i = 0; i < colCount; i++)
                maxLengths[i] = headers[i].Length;

            foreach (var row in rows.Take(100))
            {
                for (var i = 0; i < Math.Min(row.Count, colCount); i++)
                {
                    var len = (row[i] ?? string.Empty).Length;
                    if (len > maxLengths[i]) maxLengths[i] = len;
                }
            }

            for (var i = 0; i < colCount; i++)
                maxLengths[i] = Math.Min(maxLengths[i], 40);

            var totalLen = maxLengths.Sum();
            if (totalLen == 0) totalLen = 1;

            var widths = maxLengths.Select(l => Math.Max((int)((double)l / totalLen * usableWidth), 30)).ToArray();

            // Ensure total width doesn't exceed usable width
            var totalWidth = widths.Sum();
            if (totalWidth > usableWidth)
            {
                var scale = (double)usableWidth / totalWidth;
                widths = widths.Select(w => Math.Max((int)(w * scale), 25)).ToArray();
            }

            return widths;
        }

        private static List<List<PdfTableRow>> BuildTablePages(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> dataRows, int[] colWidths, int totalRowCount)
        {
            const int pageHeight = 842;
            const int topMargin = 64; // below header bar
            const int bottomMargin = 50;
            const int titleBlockHeight = 40;
            const int rowHeight = 16;
            const int headerRowHeight = 20;

            var usableHeight = pageHeight - topMargin - bottomMargin;

            var pages = new List<List<PdfTableRow>>();
            var currentPage = new List<PdfTableRow>();
            var currentY = usableHeight;
            var isFirstPage = true;

            // Add title on first page
            currentY -= titleBlockHeight;

            // Add header row
            currentPage.Add(new PdfTableRow(headers.Select(h => h).ToList(), true));
            currentY -= headerRowHeight;

            foreach (var row in dataRows)
            {
                if (currentY - rowHeight < 0)
                {
                    pages.Add(currentPage);
                    currentPage = new List<PdfTableRow>();
                    currentY = usableHeight;
                    isFirstPage = false;

                    // Repeat header on new page
                    currentPage.Add(new PdfTableRow(headers.Select(h => h).ToList(), true));
                    currentY -= headerRowHeight;
                }

                var cells = row.Select(v => (v ?? string.Empty)).ToList();
                currentPage.Add(new PdfTableRow(cells, false));
                currentY -= rowHeight;
            }

            if (totalRowCount > 500)
            {
                currentPage.Add(new PdfTableRow(
                    new List<string> { $"... and {totalRowCount - 500:N0} more records (truncated)" },
                    false));
            }

            if (currentPage.Count > 0)
                pages.Add(currentPage);

            if (pages.Count == 0)
                pages.Add(new List<PdfTableRow> { new(new List<string> { "No data." }, false) });

            return pages;
        }

        private static byte[] BuildStructuredPdf(IReadOnlyList<List<PdfTableRow>> pages, string reportTitle)
        {
            var objects = new List<string>();
            var pageObjectIds = new List<int>();

            var fontRegId = 3 + (pages.Count * 2);
            var fontBoldId = fontRegId + 1;

            objects.Add("1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n");

            for (var i = 0; i < pages.Count; i++)
            {
                var pageObjectId = 3 + (i * 2);
                var contentObjectId = pageObjectId + 1;
                pageObjectIds.Add(pageObjectId);

                var stream = BuildTablePageStream(pages[i], reportTitle, i + 1, pages.Count, i == 0);
                objects.Add($"{pageObjectId} 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents {contentObjectId} 0 R /Resources << /Font << /F1 {fontRegId} 0 R /F2 {fontBoldId} 0 R >> >> >>endobj\n");
                objects.Add($"{contentObjectId} 0 obj<< /Length {Encoding.ASCII.GetByteCount(stream)} >>stream\n{stream}\nendstream endobj\n");
            }

            var kids = string.Join(' ', pageObjectIds.Select(id => $"{id} 0 R"));
            objects.Insert(1, $"2 0 obj<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>endobj\n");

            objects.Add($"{fontRegId} 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n");
            objects.Add($"{fontBoldId} 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>endobj\n");

            return SerializePdf(objects);
        }

        private static string BuildTablePageStream(IReadOnlyList<PdfTableRow> tableRows, string reportTitle, int pageNum, int totalPages, bool isFirstPage)
        {
            var sb = new StringBuilder();
            const int leftMargin = 36;
            const int rightMargin = 559;
            const int tableWidth = 523; // rightMargin - leftMargin
            const int rowHeight = 16;
            const int headerRowHeight = 20;
            const int fontSize = 7;
            const int headerFontSize = 8;

            // ── Header bar (blue stripe) ──
            sb.AppendLine("0.102 0.451 0.910 rg");
            sb.AppendLine("0 820 595 22 re f");
            sb.AppendLine("BT 1 1 1 rg /F2 10 Tf 36 826 Td");
            sb.Append('(').Append(EscapePdf(CompanyName)).AppendLine(") Tj ET");

            // ── Header separator ──
            sb.AppendLine("0.855 0.863 0.878 RG 0.5 w");
            sb.AppendLine("36 815 m 559 815 l S");

            // ── Footer ──
            sb.AppendLine("BT 0.373 0.388 0.416 rg /F1 8 Tf");
            sb.AppendLine($"36 25 Td ({EscapePdf($"Page {pageNum} of {totalPages}")}) Tj ET");
            sb.AppendLine("BT 0.373 0.388 0.416 rg /F1 8 Tf");
            sb.AppendLine($"430 25 Td ({EscapePdf("Confidential")}) Tj ET");
            sb.AppendLine("0.855 0.863 0.878 RG 36 38 m 559 38 l S");

            var currentY = 800;

            // ── Title block (first page only) ──
            if (isFirstPage)
            {
                sb.AppendLine("BT 0.102 0.451 0.910 rg /F2 14 Tf");
                sb.AppendLine($"{leftMargin} {currentY} Td ({EscapePdf(reportTitle)}) Tj ET");
                currentY -= 18;

                sb.AppendLine("BT 0.373 0.388 0.416 rg /F1 8 Tf");
                sb.AppendLine($"{leftMargin} {currentY} Td ({EscapePdf($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")}) Tj ET");
                currentY -= 16;
            }

            // Compute column widths from tableRows
            var colCount = tableRows.Where(r => r.IsHeader).Select(r => r.Cells.Count).FirstOrDefault();
            if (colCount == 0) colCount = tableRows.Select(r => r.Cells.Count).Max();

            var colWidths = ComputeColumnWidthsFromRows(tableRows, colCount, tableWidth);

            // ── Draw table ──
            foreach (var row in tableRows)
            {
                var rh = row.IsHeader ? headerRowHeight : rowHeight;

                // ── Background fill ──
                if (row.IsHeader)
                {
                    // Header row background: light gray
                    sb.AppendLine("0.941 0.945 0.949 rg"); // #f0f1f2
                    sb.AppendLine($"{leftMargin} {currentY - rh} {tableWidth} {rh} re f");
                }

                // ── Cell borders ──
                sb.AppendLine("0.855 0.863 0.878 RG"); // #dadce0
                sb.AppendLine("0.5 w");

                // Outer rectangle for the row
                sb.AppendLine($"{leftMargin} {currentY - rh} {tableWidth} {rh} re S");

                // Vertical column separators
                var xPos = leftMargin;
                for (var c = 0; c < Math.Min(row.Cells.Count, colWidths.Length) - 1; c++)
                {
                    xPos += colWidths[c];
                    sb.AppendLine($"{xPos} {currentY} m {xPos} {currentY - rh} l S");
                }

                // ── Cell text ──
                xPos = leftMargin;
                var textY = currentY - rh + 5; // baseline offset from bottom of cell
                var font = row.IsHeader ? "/F2" : "/F1";
                var fSize = row.IsHeader ? headerFontSize : fontSize;

                sb.AppendLine("0.125 0.129 0.141 rg"); // #202124
                for (var c = 0; c < Math.Min(row.Cells.Count, colWidths.Length); c++)
                {
                    var cellWidth = colWidths[c];
                    var text = row.Cells[c];

                    // Truncate text to fit cell width (approx 2 chars per PDF unit at this font size)
                    var maxChars = (int)(cellWidth / (fSize * 0.45));
                    if (text.Length > maxChars && maxChars > 3)
                        text = text[..(maxChars - 2)] + "..";

                    sb.AppendLine($"BT {font} {fSize} Tf {xPos + 3} {textY} Td ({EscapePdf(text)}) Tj ET");
                    xPos += cellWidth;
                }

                currentY -= rh;

                // Page safety: stop if we're too close to footer
                if (currentY < 50) break;
            }

            return sb.ToString();
        }

        private static int[] ComputeColumnWidthsFromRows(IReadOnlyList<PdfTableRow> rows, int colCount, int tableWidth)
        {
            var maxLengths = new int[colCount];

            foreach (var row in rows.Take(50))
            {
                for (var i = 0; i < Math.Min(row.Cells.Count, colCount); i++)
                {
                    var len = row.Cells[i].Length;
                    if (len > maxLengths[i]) maxLengths[i] = len;
                }
            }

            for (var i = 0; i < colCount; i++)
                maxLengths[i] = Math.Max(maxLengths[i], 3);

            var totalLen = maxLengths.Sum();
            if (totalLen == 0) totalLen = 1;

            var widths = maxLengths.Select(l => Math.Max((int)((double)l / totalLen * tableWidth), 25)).ToArray();

            // Normalize to fit exactly
            var currentTotal = widths.Sum();
            if (currentTotal != tableWidth && widths.Length > 0)
                widths[^1] += (tableWidth - currentTotal);

            return widths;
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

        private record PdfTableRow(IList<string> Cells, bool IsHeader);
    }
}
