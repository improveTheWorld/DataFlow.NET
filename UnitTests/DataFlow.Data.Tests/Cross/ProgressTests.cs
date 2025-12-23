using System.Reflection;

using DataFlow.Data;
using DataFlow.Data.Tests.Generators;
using DataFlow.Data.Tests.Utilities;
using Xunit;

namespace DataFlow.Data.Tests.Cross;

public class ProgressTests
{
    public record Node
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public bool ok { get; set; }
    }

    private readonly DataSetGenerator.GeneratedFiles _files;
    public ProgressTests()
    {
        var root = TempFileHelper.CreateTempDirectory("Progress");
        var cfg = new DataGenConfig { CsvRows = 5_000, JsonArrayLength = 4_000, YamlDocuments = 2_000, TextLines = 3_000 };
        _files = DataSetGenerator.EnsureGenerated(root, cfg);
    }

    // Helper: Try to set ProgressInterval (TimeSpan) to zero via reflection if available.
    private void TryForceProgressInterval(object options)
    {
        var prop = options.GetType().GetProperty("ProgressInterval", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(TimeSpan) && prop.CanWrite)
            prop.SetValue(options, TimeSpan.Zero);
    }

    [Fact]
    public async Task Csv_Progress_Emitted()
    {
        var progress = new ProgressCapture<ReaderProgress>();
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            Progress = progress,
            ErrorAction = ReaderErrorAction.Skip,
            ProgressRecordInterval = 100,
            ProgressTimeInterval = TimeSpan.FromMilliseconds(200)
        };
        TryForceProgressInterval(opts); // if this helper sets even smaller values, fine.

        int n = 0;
        await foreach (var _ in Read.Csv<dynamic>(_files.CsvPath, opts))
            if (++n > 2000) break;

        Assert.True(progress.Events.Count > 0, "Expected at least one progress event for CSV.");
    }

    [Fact]
    public async Task Json_Progress_Emitted()
    {
        var progress = new ProgressCapture<object>();
        var opts = new JsonReadOptions<dynamic>
        {
            RequireArrayRoot = true,
            Progress = progress
        };
        TryForceProgressInterval(opts);
        int n = 0;
        await foreach (var _ in Read.Json<dynamic>(_files.JsonArrayPath, opts))
            if (++n > 1500) break;
        Assert.True(progress.Events.Count > 0, "No JSON progress events captured.");
    }

    [Fact]
    public async Task Yaml_Progress_Emitted()
    {
        var progress = new ProgressCapture<object>();
        var opts = new YamlReadOptions<dynamic>
        {
            Progress = progress,
            RestrictTypes = false,
            ErrorAction = ReaderErrorAction.Skip,
            ProgressRecordInterval = 100,              // ensure a count trigger
            ProgressTimeInterval = TimeSpan.FromSeconds(60) // push time trigger out of the way

        };
        TryForceProgressInterval(opts);
        int n = 0;
        await foreach (var _ in Read.Yaml<dynamic>(_files.YamlSequencePath, opts))
            if (++n > 1200) break;
        Assert.True(progress.Events.Count > 0);
    }

}
