namespace ERP.BLL.Reporting.Dtos;

/// <summary>
/// Encapsulates the result of a report export operation, including the file bytes,
/// content type, and suggested file name.
/// </summary>
public sealed class ExportResult
{
    public byte[] FileBytes { get; init; } = Array.Empty<byte>();
    public string ContentType { get; init; } = "application/octet-stream";
    public string FileName { get; init; } = "report";
}

/// <summary>
/// Unified export format enum used across the reporting pipeline.
/// Kept in BLL so both PL and background job layers can reference it consistently.
/// </summary>
public enum ExportFormat
{
    Pdf = 0,
    Html = 1,
    Csv = 2,
    Excel = 3
}
