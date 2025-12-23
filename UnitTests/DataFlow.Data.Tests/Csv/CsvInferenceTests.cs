using DataFlow.Data;
using DataFlow.Data.Tests.Utilities;
using Xunit;

namespace DataFlow.Data.Tests.Csv;

public class CsvInferenceTests
{
    [Fact]
    public async Task Inference_Demotes_OnRuntime_Failure()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "X,Y\n1,2\n2,3\nA,ZZZ\n5,6");
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            InferSchema = true,
            SchemaInferenceMode = SchemaInferenceMode.ColumnNamesAndTypes,
            SchemaInferenceSampleRows = 2,
            FieldTypeInference = FieldTypeInferenceMode.Primitive
        };

        var values = new List<dynamic>();
        await foreach (var rec in Read.Csv<dynamic>(path, opts))
            values.Add(rec);

        Assert.NotNull(opts.InferredTypes);
        // After encountering non-numeric 'A', column X demoted to string
        var xType = opts.InferredTypes![0];
        Assert.Equal(typeof(string), xType);
    }

    [Fact]
    public async Task LeadingZero_Preserved_AsString()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "ID\n0123\n0456\n0009");
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            InferSchema = true,
            SchemaInferenceMode = SchemaInferenceMode.ColumnNamesAndTypes,
            SchemaInferenceSampleRows = 3
        };
        await foreach (var _ in Read.Csv<dynamic>(path, opts)) { }
        Assert.Equal(typeof(string), opts.InferredTypes![0]);
    }
}