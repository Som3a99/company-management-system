using System.Text;

namespace ERP.BLL.Reporting.Services;

/// <summary>
/// Low-level report file builder that generates CSV, styled HTML, and properly-structured PDF files.
/// The PDF generator produces valid PDF-1.4 documents with landscape/portrait orientation,
/// professional table layout, text wrapping, alternating row shading, header repetition,
/// page numbers, and company branding.
/// </summary>
public static class ReportFileBuilder
{
    private const string CompanyName = "ERP Company Management System";
    private const string PrimaryColor = "#1a73e8";
    private const string HeaderBg = "#f8f9fa";
    private const string AltRowBg = "#f1f3f4";
    private const string BorderColor = "#dadce0";

    // ═══════════════════════════════════════════════════════════════
    //  CSV
    // ═══════════════════════════════════════════════════════════════

    public static byte[] ToCsv(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Report,{EscapeCsv(title)}");
        sb.AppendLine($"Generated At (UTC),{EscapeCsv(DateTime.UtcNow.ToString("u"))}");
        sb.AppendLine($"Total Records,{rows.Count}");
        sb.AppendLine();
        sb.AppendLine(string.Join(',', headers.Select(EscapeCsv)));

        foreach (var row in rows)
            sb.AppendLine(string.Join(',', row.Select(v => EscapeCsv(v ?? string.Empty))));

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Styled HTML (for download / browser viewing)
    // ═══════════════════════════════════════════════════════════════

    public static byte[] ToStyledHtml(string title, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows, string? filterSummary = null)
    {
        var sb = new StringBuilder(4096);
        sb.Append("<!DOCTYPE html><html lang='en'><head><meta charset='utf-8'>");
        sb.Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        sb.Append($"<title>{HtmlEnc(title)}</title>");
        sb.Append("<style>");
        sb.Append("*, *::before, *::after { box-sizing: border-box; }");
        sb.Append("body { font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, 'Helvetica Neue', Arial, sans-serif; margin: 0; padding: 0; color: #202124; background: #fff; }");
        sb.Append($".header {{ background: linear-gradient(135deg, {PrimaryColor}, #1557b0); color: #fff; padding: 24px 32px; }}");
        sb.Append(".header h1 { margin: 0 0 4px 0; font-size: 14px; font-weight: 400; opacity: 0.85; }");
        sb.Append(".header h2 { margin: 0; font-size: 22px; font-weight: 600; }");
        sb.Append(".content { padding: 24px 32px; }");
        sb.Append(".meta { color: #5f6368; font-size: 12px; margin-bottom: 6px; }");
        sb.Append(".filter-info { color: #5f6368; font-size: 12px; margin-bottom: 20px; font-style: italic; }");
        sb.Append($"table {{ border-collapse: collapse; width: 100%; border: 1px solid {BorderColor}; font-size: 13px; margin-bottom: 16px; table-layout: fixed; }}");
        sb.Append($"th {{ background: {HeaderBg}; color: #202124; font-weight: 600; text-align: left; padding: 10px 14px; border: 1px solid {BorderColor}; white-space: nowrap; position: sticky; top: 0; }}");
        sb.Append($"td {{ padding: 8px 14px; border: 1px solid {BorderColor}; vertical-align: top; word-wrap: break-word; overflow-wrap: break-word; }}");
        sb.Append($"tr:nth-child(even) td {{ background: {AltRowBg}; }}");
        sb.Append("tr:hover td { background: #e8f0fe; }");
        sb.Append($".footer {{ padding: 16px 32px; border-top: 1px solid {BorderColor}; font-size: 11px; color: #5f6368; }}");
        sb.Append(".empty-msg { padding: 32px; text-align: center; color: #5f6368; font-size: 14px; }");
        sb.Append("@media print { .header { -webkit-print-color-adjust: exact; print-color-adjust: exact; } }");
        sb.Append("</style></head><body>");

        sb.Append("<div class='header'>");
        sb.Append($"<h1>{HtmlEnc(CompanyName)}</h1>");
        sb.Append($"<h2>{HtmlEnc(title)}</h2>");
        sb.Append("</div>");

        sb.Append("<div class='content'>");
        sb.Append($"<div class='meta'>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC &bull; {rows.Count:N0} record(s)</div>");

        if (!string.IsNullOrWhiteSpace(filterSummary))
            sb.Append($"<div class='filter-info'>Filters: {HtmlEnc(filterSummary)}</div>");

        if (rows.Count == 0)
        {
            sb.Append("<div class='empty-msg'>No records match the applied filters.</div>");
        }
        else
        {
            sb.Append("<table><thead><tr>");
            foreach (var h in headers)
                sb.Append($"<th>{HtmlEnc(h)}</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var row in rows)
            {
                sb.Append("<tr>");
                foreach (var c in row)
                    sb.Append($"<td>{HtmlEnc(c ?? string.Empty)}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table>");
        }

        sb.Append("</div>");
        sb.Append($"<div class='footer'>{HtmlEnc(CompanyName)} &mdash; Confidential &bull; {DateTime.UtcNow:yyyy-MM-dd}</div>");
        sb.Append("</body></html>");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    // ═══════════════════════════════════════════════════════════════
    //  Excel-compatible HTML (.xls)
    // ═══════════════════════════════════════════════════════════════

    public static byte[] ToExcelHtml(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var sb = new StringBuilder(4096);
        sb.Append("<!DOCTYPE html><html xmlns:o='urn:schemas-microsoft-com:office:office' ");
        sb.Append("xmlns:x='urn:schemas-microsoft-com:office:excel' ");
        sb.Append("xmlns='http://www.w3.org/TR/REC-html40'>");
        sb.Append("<head><meta charset='utf-8'>");
        sb.Append("<!--[if gte mso 9]><xml><x:ExcelWorkbook><x:ExcelWorksheets><x:ExcelWorksheet>");
        sb.Append($"<x:Name>{HtmlEnc(title)}</x:Name>");
        sb.Append("<x:WorksheetOptions><x:Panes></x:Panes></x:WorksheetOptions>");
        sb.Append("</x:ExcelWorksheet></x:ExcelWorksheets></x:ExcelWorkbook></xml><![endif]-->");
        sb.Append("<style>");
        sb.Append("body { font-family: Calibri, 'Segoe UI', Arial, sans-serif; }");
        sb.Append($"table {{ border-collapse: collapse; width: 100%; font-size: 11pt; }}");
        sb.Append($"th {{ background: {HeaderBg}; font-weight: bold; text-align: left; padding: 8px 10px; border: 1px solid {BorderColor}; }}");
        sb.Append($"td {{ padding: 6px 10px; border: 1px solid {BorderColor}; vertical-align: top; white-space: pre-wrap; }}");
        sb.Append($"tr:nth-child(even) td {{ background: {AltRowBg}; }}");
        sb.Append("</style></head><body>");

        sb.Append($"<h2>{HtmlEnc(title)}</h2>");
        sb.Append($"<p style='font-size:10pt;color:#5f6368;'>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC &bull; {rows.Count:N0} record(s)</p>");

        sb.Append("<table><thead><tr>");
        foreach (var h in headers)
            sb.Append($"<th>{HtmlEnc(h)}</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var row in rows)
        {
            sb.Append("<tr>");
            foreach (var c in row)
                sb.Append($"<td>{HtmlEnc(c ?? string.Empty)}</td>");
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table>");
        sb.Append($"<p style='font-size:9pt;color:#5f6368;'>{HtmlEnc(CompanyName)} — Confidential</p>");
        sb.Append("</body></html>");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    // ═══════════════════════════════════════════════════════════════
    //  PDF — Properly-structured PDF-1.4 document
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates a valid PDF-1.4 document with professional table layout.
    /// Uses landscape orientation for wide reports (≥ 6 columns) and portrait otherwise.
    /// Features: text wrapping, alternating row shading, header repetition on each page,
    /// page numbers, company branding, and filter summary display.
    /// </summary>
    public static byte[] ToPdf(string title, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows, string? filterSummary = null)
    {
        // Orientation: landscape for ≥ 6 columns, portrait otherwise
        bool landscape = headers.Count >= 6;
        int pageW = landscape ? 842 : 595;
        int pageH = landscape ? 595 : 842;

        const int marginL = 40;
        const int marginR = 40;
        const int marginT = 50;
        const int marginB = 45;
        int usableW = pageW - marginL - marginR;

        var colWidths = ComputeSmartColumnWidths(headers, rows, usableW);

        // Build content for each page as content-stream byte arrays
        var pageStreams = BuildPdfPageStreams(
            title, headers, rows, colWidths,
            pageW, pageH, marginL, marginR, marginT, marginB,
            filterSummary);

        // Assemble into a valid PDF document with proper xref and structure
        return AssemblePdfDocument(pageStreams, pageW, pageH);
    }

    /// <summary>
    /// Legacy entry-point preserved for backward compatibility.
    /// New code should call <see cref="ToPdf"/> instead.
    /// </summary>
    public static byte[] ToSimplePdf(string title, IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows)
        => ToPdf(title, headers, rows);

    // ───────────────────────────────────────────────────────────────
    //  PDF — Column-width calculation
    // ───────────────────────────────────────────────────────────────

    private static int[] ComputeSmartColumnWidths(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        int usableWidth)
    {
        int colCount = headers.Count;
        if (colCount == 0)
            return Array.Empty<int>();

        var idealWidths = new double[colCount];

        // Seed with header text widths (bold 8pt ≈ 4.4 pts/char + cell padding)
        for (int i = 0; i < colCount; i++)
            idealWidths[i] = headers[i].Length * 4.4 + 12;

        // Sample data rows to estimate content widths (regular 7pt ≈ 3.7 pts/char)
        foreach (var row in rows.Take(200))
        {
            for (int i = 0; i < Math.Min(row.Count, colCount); i++)
            {
                var len = (row[i] ?? "").Length;
                var textWidth = len * 3.7 + 10;
                if (textWidth > idealWidths[i])
                    idealWidths[i] = textWidth;
            }
        }

        // Cap each column to 35% of usable width so no single column dominates
        double maxColWidth = usableWidth * 0.35;
        for (int i = 0; i < colCount; i++)
            idealWidths[i] = Math.Min(idealWidths[i], maxColWidth);

        // Enforce minimum 35pt per column for readability
        for (int i = 0; i < colCount; i++)
            idealWidths[i] = Math.Max(idealWidths[i], 35);

        // Scale proportionally to fit available width
        double total = idealWidths.Sum();
        var result = new int[colCount];
        for (int i = 0; i < colCount; i++)
            result[i] = Math.Max((int)(idealWidths[i] / total * usableWidth), 30);

        // Absorb rounding errors into the last column
        int actualTotal = result.Sum();
        if (result.Length > 0)
            result[^1] += (usableWidth - actualTotal);

        return result;
    }

    // ───────────────────────────────────────────────────────────────
    //  PDF — Page-stream generation
    // ───────────────────────────────────────────────────────────────

    private static List<byte[]> BuildPdfPageStreams(
        string title,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        int[] colWidths,
        int pageW, int pageH,
        int ml, int mr, int mt, int mb,
        string? filterSummary)
    {
        const int headerBarH = 22;
        const int dataFontSize = 7;
        const int headerFontSize = 8;
        const int titleFontSize = 13;
        const int lineSpacing = 10; // dataFontSize + 3
        const int cellPadY = 3;
        const int cellPadX = 4;
        const int tableHeaderRowH = 18;
        const int footerZoneH = 30;

        int usableW = pageW - ml - mr;
        int contentTop = pageH - mt;
        int contentBottom = mb + footerZoneH;

        var allPageStreams = new List<byte[]>();
        var sb = new StringBuilder(8192);
        int pageNum = 0;
        int rowIndex = 0;
        bool isFirstPage = true;

        while (rowIndex <= rows.Count) // <= to ensure at least one page for empty datasets
        {
            pageNum++;
            sb.Clear();

            // ── Blue header bar at top of every page ──
            sb.AppendLine("q");
            sb.AppendLine("0.102 0.451 0.910 rg");
            sb.AppendLine($"0 {pageH - headerBarH} {pageW} {headerBarH} re f");
            sb.AppendLine($"BT 1 1 1 rg /F2 9 Tf {ml} {pageH - headerBarH + 7} Td ({EscapePdf(CompanyName)}) Tj ET");
            sb.AppendLine("Q");

            int currentY = contentTop;

            // ── Title block (first page only) ──
            if (isFirstPage)
            {
                sb.AppendLine($"BT 0.102 0.451 0.910 rg /F2 {titleFontSize} Tf {ml} {currentY} Td ({EscapePdf(title)}) Tj ET");
                currentY -= titleFontSize + 5;

                var metaText = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  |  {rows.Count:N0} record(s)";
                sb.AppendLine($"BT 0.373 0.388 0.416 rg /F1 7 Tf {ml} {currentY} Td ({EscapePdf(metaText)}) Tj ET");
                currentY -= 12;

                if (!string.IsNullOrWhiteSpace(filterSummary))
                {
                    sb.AppendLine($"BT 0.373 0.388 0.416 rg /F1 7 Tf {ml} {currentY} Td ({EscapePdf("Filters: " + filterSummary)}) Tj ET");
                    currentY -= 12;
                }

                currentY -= 4;
            }

            // ── Table header row ──
            RenderTableHeaderRow(sb, headers, colWidths, ml, ref currentY, tableHeaderRowH, headerFontSize, cellPadX, usableW);

            // ── Data rows ──
            if (rows.Count == 0 && isFirstPage)
            {
                int emptyRowH = 20;
                sb.AppendLine($"0.855 0.863 0.878 RG 0.5 w {ml} {currentY - emptyRowH} {usableW} {emptyRowH} re S");
                sb.AppendLine($"BT 0.373 0.388 0.416 rg /F1 {dataFontSize} Tf {ml + cellPadX} {currentY - dataFontSize - cellPadY} Td ({EscapePdf("No records match the applied filters.")}) Tj ET");
                currentY -= emptyRowH;
                rowIndex = 1; // ensure loop exits
            }
            else
            {
                int dataRowNum = rowIndex; // preserve global row number for alternating colors
                while (rowIndex < rows.Count)
                {
                    var row = rows[rowIndex];
                    var cells = row.Select(v => v ?? string.Empty).ToList();

                    // Calculate dynamic row height based on text wrapping
                    int maxLines = 1;
                    for (int c = 0; c < Math.Min(cells.Count, colWidths.Length); c++)
                    {
                        int maxChars = MaxCharsForWidth(colWidths[c], dataFontSize, cellPadX);
                        int lines = cells[c].Length <= maxChars ? 1 : (int)Math.Ceiling((double)cells[c].Length / Math.Max(maxChars, 1));
                        if (lines > maxLines) maxLines = lines;
                    }
                    maxLines = Math.Min(maxLines, 6);
                    int rowH = Math.Max(14, maxLines * lineSpacing + cellPadY * 2);

                    // Check if row fits on current page
                    if (currentY - rowH < contentBottom)
                        break;

                    // ── Alternating row background ──
                    if (dataRowNum % 2 == 1)
                    {
                        sb.AppendLine("0.945 0.949 0.953 rg");
                        sb.AppendLine($"{ml} {currentY - rowH} {usableW} {rowH} re f");
                    }

                    // ── Row border ──
                    sb.AppendLine("0.855 0.863 0.878 RG 0.5 w");
                    sb.AppendLine($"{ml} {currentY - rowH} {usableW} {rowH} re S");

                    // ── Column separators ──
                    int sepX = ml;
                    for (int c = 0; c < Math.Min(cells.Count, colWidths.Length) - 1; c++)
                    {
                        sepX += colWidths[c];
                        sb.AppendLine($"{sepX} {currentY} m {sepX} {currentY - rowH} l S");
                    }

                    // ── Cell text with word wrapping ──
                    int cellX = ml;
                    sb.AppendLine("0.125 0.129 0.141 rg");
                    for (int c = 0; c < Math.Min(cells.Count, colWidths.Length); c++)
                    {
                        int maxChars = MaxCharsForWidth(colWidths[c], dataFontSize, cellPadX);
                        var wrappedLines = WrapText(cells[c], maxChars, 6);

                        for (int li = 0; li < wrappedLines.Count; li++)
                        {
                            int textY = currentY - dataFontSize - cellPadY - (li * lineSpacing);
                            if (textY < currentY - rowH + 2) break;
                            sb.AppendLine($"BT /F1 {dataFontSize} Tf {cellX + cellPadX} {textY} Td ({EscapePdf(wrappedLines[li])}) Tj ET");
                        }

                        cellX += colWidths[c];
                    }

                    currentY -= rowH;
                    rowIndex++;
                    dataRowNum++;
                }
            }

            // ── Footer (page number uses placeholder replaced after all pages built) ──
            RenderFooter(sb, pageNum, ml, mr, pageW, footerZoneH, mb);

            allPageStreams.Add(Encoding.Latin1.GetBytes(sb.ToString()));
            isFirstPage = false;

            if (rows.Count == 0 || rowIndex >= rows.Count)
                break;
        }

        // Ensure at least one page
        if (allPageStreams.Count == 0)
        {
            sb.Clear();
            sb.AppendLine("q 0.102 0.451 0.910 rg");
            sb.AppendLine($"0 {pageH - 22} {pageW} 22 re f");
            sb.AppendLine($"BT 1 1 1 rg /F2 9 Tf {ml} {pageH - 15} Td ({EscapePdf(CompanyName)}) Tj ET Q");
            sb.AppendLine($"BT 0.373 0.388 0.416 rg /F1 8 Tf {ml} {contentTop} Td ({EscapePdf("No data available.")}) Tj ET");
            RenderFooter(sb, 1, ml, mr, pageW, footerZoneH, mb);
            allPageStreams.Add(Encoding.Latin1.GetBytes(sb.ToString()));
        }

        // Replace __TOTAL__ placeholder with actual page count
        return UpdatePageTotals(allPageStreams, allPageStreams.Count);
    }

    private static void RenderTableHeaderRow(
        StringBuilder sb,
        IReadOnlyList<string> headers,
        int[] colWidths,
        int ml,
        ref int currentY,
        int rowH,
        int fontSize,
        int cellPadX,
        int usableW)
    {
        // Header background fill
        sb.AppendLine("0.933 0.937 0.941 rg");
        sb.AppendLine($"{ml} {currentY - rowH} {usableW} {rowH} re f");

        // Header border
        sb.AppendLine("0.835 0.843 0.859 RG 0.5 w");
        sb.AppendLine($"{ml} {currentY - rowH} {usableW} {rowH} re S");

        // Column separators
        int sepX = ml;
        for (int c = 0; c < colWidths.Length - 1; c++)
        {
            sepX += colWidths[c];
            sb.AppendLine($"{sepX} {currentY} m {sepX} {currentY - rowH} l S");
        }

        // Header cell text (bold font)
        int cellX = ml;
        sb.AppendLine("0.125 0.129 0.141 rg");
        for (int c = 0; c < Math.Min(headers.Count, colWidths.Length); c++)
        {
            int maxChars = MaxCharsForWidth(colWidths[c], fontSize, cellPadX);
            string text = headers[c].Length > maxChars ? headers[c][..maxChars] : headers[c];
            int textY = currentY - fontSize - 4;
            sb.AppendLine($"BT /F2 {fontSize} Tf {cellX + cellPadX} {textY} Td ({EscapePdf(text)}) Tj ET");
            cellX += colWidths[c];
        }

        currentY -= rowH;
    }

    private static void RenderFooter(StringBuilder sb, int pageNum, int ml, int mr, int pageW, int footerZoneH, int mb)
    {
        int lineY = mb + footerZoneH - 8;
        int textY = mb + 8;

        // Footer separator line
        sb.AppendLine("0.855 0.863 0.878 RG 0.5 w");
        sb.AppendLine($"{ml} {lineY} m {pageW - mr} {lineY} l S");

        // Page number with placeholder that gets replaced after all pages are built
        sb.AppendLine($"BT 0.373 0.388 0.416 rg /F1 7 Tf {ml} {textY} Td ({EscapePdf($"Page {pageNum} of __TOTAL__")}) Tj ET");

        // Company + confidential notice (right-aligned estimate)
        var rightText = $"{CompanyName} — Confidential — {DateTime.UtcNow:yyyy-MM-dd}";
        int approxWidth = (int)(rightText.Length * 3.2);
        int rightX = pageW - mr - approxWidth;
        sb.AppendLine($"BT 0.373 0.388 0.416 rg /F1 7 Tf {rightX} {textY} Td ({EscapePdf(rightText)}) Tj ET");
    }

    /// <summary>
    /// Replace __TOTAL__ placeholders in page streams with the actual total page count.
    /// </summary>
    private static List<byte[]> UpdatePageTotals(List<byte[]> pageStreams, int totalPages)
    {
        var placeholder = Encoding.Latin1.GetBytes("__TOTAL__");
        var replacement = Encoding.Latin1.GetBytes(totalPages.ToString());

        var result = new List<byte[]>(pageStreams.Count);
        foreach (var stream in pageStreams)
            result.Add(ReplaceBytesInStream(stream, placeholder, replacement));
        return result;
    }

    private static byte[] ReplaceBytesInStream(byte[] source, byte[] pattern, byte[] replacement)
    {
        var positions = new List<int>();
        for (int i = 0; i <= source.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) positions.Add(i);
        }

        if (positions.Count == 0) return source;

        var result = new byte[source.Length + positions.Count * (replacement.Length - pattern.Length)];
        int srcPos = 0, dstPos = 0;
        foreach (var pos in positions)
        {
            int copyLen = pos - srcPos;
            Buffer.BlockCopy(source, srcPos, result, dstPos, copyLen);
            dstPos += copyLen;
            Buffer.BlockCopy(replacement, 0, result, dstPos, replacement.Length);
            dstPos += replacement.Length;
            srcPos = pos + pattern.Length;
        }
        Buffer.BlockCopy(source, srcPos, result, dstPos, source.Length - srcPos);

        return result;
    }

    // ───────────────────────────────────────────────────────────────
    //  PDF — Document assembly (proper PDF-1.4 structure)
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Assembles a valid PDF-1.4 file from pre-built content streams using MemoryStream
    /// for accurate byte-offset tracking. Each object is separated by proper whitespace
    /// per the PDF specification.
    /// </summary>
    private static byte[] AssemblePdfDocument(IReadOnlyList<byte[]> pageContentStreams, int pageW, int pageH)
    {
        using var ms = new MemoryStream(32768);
        var offsets = new Dictionary<int, long>();
        int nextObjId = 1;

        // ── %PDF header + binary marker ──
        PdfWrite(ms, "%PDF-1.4\n");
        ms.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });

        // ── Object 1: Catalog ──
        int catalogId = nextObjId++;
        offsets[catalogId] = ms.Position;
        PdfWrite(ms, $"{catalogId} 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Reserve slot 2 for Pages (written later once we know all page IDs)
        int pagesId = nextObjId++;

        // ── Font objects ──
        int fontRegId = nextObjId++;
        offsets[fontRegId] = ms.Position;
        PdfWrite(ms, $"{fontRegId} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        int fontBoldId = nextObjId++;
        offsets[fontBoldId] = ms.Position;
        PdfWrite(ms, $"{fontBoldId} 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        // ── Page + content-stream object pairs ──
        var pageObjIds = new List<int>();
        foreach (var streamBytes in pageContentStreams)
        {
            // Content-stream object
            int contentId = nextObjId++;
            offsets[contentId] = ms.Position;
            PdfWrite(ms, $"{contentId} 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
            ms.Write(streamBytes);
            PdfWrite(ms, "\nendstream\nendobj\n");

            // Page object referencing the content stream + fonts
            int pageId = nextObjId++;
            offsets[pageId] = ms.Position;
            PdfWrite(ms,
                $"{pageId} 0 obj\n" +
                $"<< /Type /Page /Parent {pagesId} 0 R " +
                $"/MediaBox [0 0 {pageW} {pageH}] " +
                $"/Contents {contentId} 0 R " +
                $"/Resources << /Font << /F1 {fontRegId} 0 R /F2 {fontBoldId} 0 R >> >> " +
                $">>\nendobj\n");

            pageObjIds.Add(pageId);
        }

        // ── Object 2: Pages dictionary ──
        offsets[pagesId] = ms.Position;
        var kids = string.Join(" ", pageObjIds.Select(id => $"{id} 0 R"));
        PdfWrite(ms, $"{pagesId} 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {pageObjIds.Count} >>\nendobj\n");

        // ── Cross-reference table (each entry exactly 20 bytes) ──
        int totalObjects = nextObjId - 1;
        long xrefPos = ms.Position;
        PdfWrite(ms, $"xref\n0 {totalObjects + 1}\n");
        PdfWrite(ms, "0000000000 65535 f \r\n"); // Free entry for object 0
        for (int i = 1; i <= totalObjects; i++)
        {
            long offset = offsets.ContainsKey(i) ? offsets[i] : 0;
            PdfWrite(ms, $"{offset:D10} 00000 n \r\n");
        }

        // ── Trailer ──
        PdfWrite(ms, $"trailer\n<< /Root {catalogId} 0 R /Size {totalObjects + 1} >>\n");
        PdfWrite(ms, $"startxref\n{xrefPos}\n%%EOF\n");

        return ms.ToArray();
    }

    private static void PdfWrite(MemoryStream ms, string text)
    {
        ms.Write(Encoding.Latin1.GetBytes(text));
    }

    // ───────────────────────────────────────────────────────────────
    //  Text utilities
    // ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the maximum number of characters that fit within a PDF column cell.
    /// Based on Helvetica average character width ≈ 0.52 × fontSize.
    /// </summary>
    private static int MaxCharsForWidth(int columnWidthPt, int fontSizePt, int cellPaddingPt = 4)
    {
        double availableWidth = columnWidthPt - (cellPaddingPt * 2);
        double avgCharWidth = fontSizePt * 0.52;
        return Math.Max((int)(availableWidth / avgCharWidth), 3);
    }

    /// <summary>
    /// Wraps text into multiple lines that fit within a maximum character width,
    /// breaking at word boundaries (spaces, hyphens, slashes) where possible.
    /// </summary>
    private static List<string> WrapText(string text, int maxCharsPerLine, int maxLines = 6)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string> { string.Empty };

        if (text.Length <= maxCharsPerLine)
            return new List<string> { text };

        var lines = new List<string>();
        var remaining = text.AsSpan();

        while (remaining.Length > 0 && lines.Count < maxLines)
        {
            if (remaining.Length <= maxCharsPerLine)
            {
                lines.Add(remaining.ToString());
                break;
            }

            var chunk = remaining[..maxCharsPerLine];

            // Try to break at a word boundary
            int breakAt = -1;
            for (int i = chunk.Length - 1; i >= maxCharsPerLine / 3; i--)
            {
                char ch = chunk[i];
                if (ch == ' ' || ch == '-' || ch == '/' || ch == '\\' || ch == ',' || ch == '.')
                {
                    breakAt = i + 1;
                    break;
                }
            }

            if (breakAt < 0)
                breakAt = maxCharsPerLine;

            lines.Add(remaining[..breakAt].ToString().TrimEnd());
            remaining = remaining[breakAt..].TrimStart();
        }

        // Indicate truncation on the last line if content was cut off
        if (remaining.Length > 0 && lines.Count > 0)
        {
            var last = lines[^1];
            lines[^1] = last.Length > 3 ? last[..^3] + "..." : "...";
        }

        return lines;
    }

    // ───────────────────────────────────────────────────────────────
    //  Escaping utilities
    // ───────────────────────────────────────────────────────────────

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    /// <summary>
    /// Escapes a string for use inside a PDF literal string: (text).
    /// Handles backslash, parentheses, newlines, tabs, and replaces
    /// unsupported Unicode characters with '?'.
    /// </summary>
    private static string EscapePdf(string value)
    {
        var sb = new StringBuilder(value.Length + 16);
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("    "); break;
                default:
                    if (c >= 32 && c <= 126)
                        sb.Append(c);
                    else if (c >= 160 && c <= 255)
                        sb.Append(c);
                    else if (c < 32)
                        sb.Append(' ');
                    else
                        sb.Append('?');
                    break;
            }
        }
        return sb.ToString();
    }

    private static string HtmlEnc(string value) => System.Net.WebUtility.HtmlEncode(value);
}
