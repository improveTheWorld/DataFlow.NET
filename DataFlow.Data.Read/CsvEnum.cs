// New enums supporting enhanced CSV parsing, schema & type inference.

namespace DataFlow.Data
{
    public enum CsvQuoteMode
    {
        // RFC strict: only a double quote as first char of an empty field starts a quoted field.
        RfcStrict,
        // Legacy lenient: any quote toggles quoted mode.
        Lenient,
        // Treat any illegal mid-field quote as a format error (reported via ErrorAction policy).
        ErrorOnIllegalQuote
    }

    public enum FieldTypeInferenceMode
    {
        None,       // Keep all fields as string
        Primitive,  // Built-in primitive inference (bool, int, long, decimal, double, DateTime, Guid)
        Custom      // Use CsvReadOptions.FieldValueConverter delegate
    }

    public enum SchemaInferenceMode
    {
        ColumnNamesOnly,
        ColumnNamesAndTypes
    }
}
