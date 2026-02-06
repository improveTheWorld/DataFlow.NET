using System.Runtime.CompilerServices;
using ClosedXML.Excel;

namespace DataFlow;

public static partial class Read
{
    /// <summary>
    /// Converts an Excel file (.xlsx, .xls, etc.) into temporary CSV files, one per sheet.
    /// Returns paths to the temporary CSV files that will be automatically cleaned up.
    /// </summary>
    /// <param name="excelFilePath">Path to the Excel file</param>
    /// <param name="options">Optional Excel conversion options</param>
    /// <returns>Array of temporary CSV file paths (one per sheet)</returns>
    public static string[] ExcelToTempCsv(
        string excelFilePath,
        ExcelConversionOptions? options = null)
    {
        if (!File.Exists(excelFilePath))
            throw new FileNotFoundException($"Excel file not found: {excelFilePath}", excelFilePath);

        options ??= ExcelConversionOptions.Default;

        var tempFiles = new List<string>();

        try
        {
            using var workbook = new XLWorkbook(excelFilePath);

            foreach (var worksheet in workbook.Worksheets)
            {
                // Skip hidden sheets if configured
                if (options.SkipHiddenSheets && worksheet.Visibility != XLWorksheetVisibility.Visible)
                    continue;

                // Skip empty sheets if configured
                if (options.SkipEmptySheets && worksheet.RangeUsed() == null)
                    continue;

                // Generate temp file path
                var tempPath = Path.Combine(
                    options.TempDirectory ?? Path.GetTempPath(),
                    $"{Path.GetFileNameWithoutExtension(excelFilePath)}_{SanitizeSheetName(worksheet.Name)}_{Guid.NewGuid():N}.csv");

                // Convert sheet to CSV
                ConvertSheetToCsv(worksheet, tempPath, options);
                tempFiles.Add(tempPath);
            }

            return tempFiles.ToArray();
        }
        catch
        {
            // Clean up any temp files created before the error
            foreach (var tempFile in tempFiles)
            {
                try { File.Delete(tempFile); } catch { /* ignore cleanup errors */ }
            }
            throw;
        }
    }



    /// <summary>
    /// Reads a specific sheet from an Excel file by name.
    /// </summary>
    public static async IAsyncEnumerable<T> ExcelSheet<T>(
        string excelFilePath,
        string sheetName,
        CsvReadOptions? csvOptions = null,
        ExcelConversionOptions? excelOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        csvOptions ??= new CsvReadOptions();
        excelOptions ??= ExcelConversionOptions.Default;

        string? tempCsvPath = null;

        try
        {
            using var workbook = new XLWorkbook(excelFilePath);

            if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var worksheet))
                throw new ArgumentException($"Sheet '{sheetName}' not found in workbook.", nameof(sheetName));

            tempCsvPath = Path.Combine(
                excelOptions.TempDirectory ?? Path.GetTempPath(),
                $"{Path.GetFileNameWithoutExtension(excelFilePath)}_{SanitizeSheetName(sheetName)}_{Guid.NewGuid():N}.csv");

            ConvertSheetToCsv(worksheet, tempCsvPath, excelOptions);

            await foreach (var record in Csv<T>(tempCsvPath, csvOptions, cancellationToken))
            {
                yield return record;
            }
        }
        finally
        {
            if (tempCsvPath != null && File.Exists(tempCsvPath))
            {
                try { File.Delete(tempCsvPath); } catch { /* ignore cleanup errors */ }
            }
        }
    }

    /// <summary>
    /// Reads a specific sheet from an Excel file by index (0-based).
    /// </summary>
    public static async IAsyncEnumerable<T> ExcelSheet<T>(
        string excelFilePath,
        int sheetIndex,
        CsvReadOptions? csvOptions = null,
        ExcelConversionOptions? excelOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        csvOptions ??= new CsvReadOptions();
        excelOptions ??= ExcelConversionOptions.Default;

        string? tempCsvPath = null;

        try
        {
            using var workbook = new XLWorkbook(excelFilePath);

            if (sheetIndex < 0 || sheetIndex >= workbook.Worksheets.Count)
                throw new ArgumentOutOfRangeException(nameof(sheetIndex),
                    $"Sheet index {sheetIndex} is out of range. Workbook has {workbook.Worksheets.Count} sheets.");

            var worksheet = workbook.Worksheets.ElementAt(sheetIndex);

            tempCsvPath = Path.Combine(
                excelOptions.TempDirectory ?? Path.GetTempPath(),
                $"{Path.GetFileNameWithoutExtension(excelFilePath)}_{SanitizeSheetName(worksheet.Name)}_{Guid.NewGuid():N}.csv");

            ConvertSheetToCsv(worksheet, tempCsvPath, excelOptions);

            await foreach (var record in Csv<T>(tempCsvPath, csvOptions, cancellationToken))
            {
                yield return record;
            }
        }
        finally
        {
            if (tempCsvPath != null && File.Exists(tempCsvPath))
            {
                try { File.Delete(tempCsvPath); } catch { /* ignore cleanup errors */ }
            }
        }
    }

    // Synchronous overloads
    public static IEnumerable<T> ExcelSheetSync<T>(
        string excelFilePath,
        string sheetName,
        CsvReadOptions? csvOptions = null,
        ExcelConversionOptions? excelOptions = null,
        CancellationToken cancellationToken = default)
    {
        csvOptions ??= new CsvReadOptions();
        excelOptions ??= ExcelConversionOptions.Default;

        string? tempCsvPath = null;

        try
        {
            using var workbook = new XLWorkbook(excelFilePath);

            if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var worksheet))
                throw new ArgumentException($"Sheet '{sheetName}' not found in workbook.", nameof(sheetName));

            tempCsvPath = Path.Combine(
                excelOptions.TempDirectory ?? Path.GetTempPath(),
                $"{Path.GetFileNameWithoutExtension(excelFilePath)}_{SanitizeSheetName(sheetName)}_{Guid.NewGuid():N}.csv");

            ConvertSheetToCsv(worksheet, tempCsvPath, excelOptions);

            foreach (var record in CsvSync<T>(tempCsvPath, csvOptions, cancellationToken))
            {
                yield return record;
            }
        }
        finally
        {
            if (tempCsvPath != null && File.Exists(tempCsvPath))
            {
                try { File.Delete(tempCsvPath); } catch { /* ignore cleanup errors */ }
            }
        }
    }

    public static IEnumerable<T> ExcelSheetSync<T>(
        string excelFilePath,
        int sheetIndex,
        CsvReadOptions? csvOptions = null,
        ExcelConversionOptions? excelOptions = null,
        CancellationToken cancellationToken = default)
    {
        csvOptions ??= new CsvReadOptions();
        excelOptions ??= ExcelConversionOptions.Default;

        string? tempCsvPath = null;

        try
        {
            using var workbook = new XLWorkbook(excelFilePath);

            if (sheetIndex < 0 || sheetIndex >= workbook.Worksheets.Count)
                throw new ArgumentOutOfRangeException(nameof(sheetIndex));

            var worksheet = workbook.Worksheets.ElementAt(sheetIndex);

            tempCsvPath = Path.Combine(
                excelOptions.TempDirectory ?? Path.GetTempPath(),
                $"{Path.GetFileNameWithoutExtension(excelFilePath)}_{SanitizeSheetName(worksheet.Name)}_{Guid.NewGuid():N}.csv");

            ConvertSheetToCsv(worksheet, tempCsvPath, excelOptions);

            foreach (var record in CsvSync<T>(tempCsvPath, csvOptions, cancellationToken))
            {
                yield return record;
            }
        }
        finally
        {
            if (tempCsvPath != null && File.Exists(tempCsvPath))
            {
                try { File.Delete(tempCsvPath); } catch { /* ignore cleanup errors */ }
            }
        }
    }

    // ===========================
    // INTERNAL HELPERS
    // ===========================

    private static void ConvertSheetToCsv(
        IXLWorksheet worksheet,
        string outputPath,
        ExcelConversionOptions options)
    {
        using var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);

        var usedRange = worksheet.RangeUsed();
        if (usedRange == null) return;

        int startRow = usedRange.FirstRow().RowNumber();
        int endRow = usedRange.LastRow().RowNumber();
        int startCol = usedRange.FirstColumn().ColumnNumber();
        int endCol = usedRange.LastColumn().ColumnNumber();

        for (int row = startRow; row <= endRow; row++)
        {
            var values = new List<string>();
            bool isEmptyRow = true;

            for (int col = startCol; col <= endCol; col++)
            {
                var cell = worksheet.Cell(row, col);
                var value = GetCellValue(cell, options);

                if (!string.IsNullOrWhiteSpace(value))
                    isEmptyRow = false;

                values.Add(EscapeCsvField(value, options.Separator));
            }



            writer.WriteLine(string.Join(options.Separator, values));
        }
    }

    private static string GetCellValue(IXLCell cell, ExcelConversionOptions options)
    {
        if (cell.IsEmpty())
            return string.Empty;

        try
        {
            return cell.DataType switch
            {
                XLDataType.DateTime => cell.GetDateTime().ToString(),
                XLDataType.TimeSpan => cell.GetTimeSpan().ToString(),
                XLDataType.Boolean => cell.GetBoolean().ToString(),
                XLDataType.Number => cell.Value.ToString(),
                _ => cell.GetString()
            };
        }
        catch
        {
            return cell.GetString();
        }
    }

    private static string EscapeCsvField(string field, string separator)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        bool needsQuoting = field.Contains(separator) ||
                           field.Contains('\"') ||
                           field.Contains('\n') ||
                           field.Contains('\r');

        if (!needsQuoting)
            return field;

        return $"\"{field.Replace("\"", "\"\"")}\"";
    }

    private static string SanitizeSheetName(string sheetName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", sheetName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}

// ===========================
// SUPPORTING TYPES
// ===========================

/// <summary>
/// Options for converting Excel files to CSV format.
/// </summary>
public sealed record ExcelConversionOptions
{
    public string Separator { get; init; } = ",";
    public bool SkipHiddenSheets { get; init; } = true;
    public bool SkipEmptySheets { get; init; } = true;
    //public bool SkipEmptyRows { get; init; } = false;
    //public bool PreserveNumericPrecision { get; init; } = true;
    //public string DateTimeFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";
    public string? TempDirectory { get; init; } = null; // null = use system temp

    public static readonly ExcelConversionOptions Default = new();
}
