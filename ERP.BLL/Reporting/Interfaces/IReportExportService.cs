using ERP.BLL.Reporting.Dtos;

namespace ERP.BLL.Reporting.Interfaces;

/// <summary>
/// Centralized report export service that converts tabular report data
/// into the requested file format with consistent styling, correct content types,
/// and professional layout across all report types.
/// </summary>
public interface IReportExportService
{
    /// <summary>
    /// Generates a report file in the specified format.
    /// </summary>
    /// <param name="title">Report title (e.g., "Tasks Report").</param>
    /// <param name="fileNameBase">Base file name without extension (e.g., "tasks-report").</param>
    /// <param name="headers">Column headers.</param>
    /// <param name="rows">Data rows (each row is a list of cell values).</param>
    /// <param name="format">Desired export format.</param>
    /// <param name="filterSummary">Optional summary of applied filters for display in the report.</param>
    /// <returns>An <see cref="ExportResult"/> with file bytes, content type, and file name.</returns>
    ExportResult Export(
        string title,
        string fileNameBase,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        ExportFormat format,
        string? filterSummary = null);
}
