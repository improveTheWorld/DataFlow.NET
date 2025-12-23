namespace DataFlow.Data.Tests.Generators;

public sealed record DataGenConfig
{
    public int Seed { get; init; } = 12345;
    public bool InjectErrors { get; init; } = true;

    public int CsvRows { get; init; } = 50_000;
    public int CsvColumns { get; init; } = 12;
    public double CsvMultilineRate { get; init; } = 0.01;
    public double CsvStrayQuoteRate { get; init; } = 0.002;
    public double CsvTrailingGarbageRate { get; init; } = 0.002;
    public double CsvLeadingZeroRate { get; init; } = 0.02;
    public double CsvLargeIntegerRate { get; init; } = 0.005;

    public int JsonArrayLength { get; init; } = 40_000;
    public double JsonInvalidElementRate { get; init; } = 0.001;
    public bool JsonIncludeSingleObject { get; init; } = true;

    public int YamlDocuments { get; init; } = 5_000;
    public double YamlAliasRate { get; init; } = 0.005;
    public double YamlCustomTagRate { get; init; } = 0.003;

    public int TextLines { get; init; } = 80_000;
    public int TextAvgLength { get; init; } = 60;
}
