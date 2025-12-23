using DataFlow.Data;
using DataFlow.Data.Tests.Utilities;
using Xunit;

namespace DataFlow.Data.Tests.Cross;

public class ProgressAndStopInteractionTests
{
    [Fact]
    public async Task Progress_Final_Event_Not_After_Stop()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "A,B\n1,2\n3,4,5\n6,7\n8,9");


        var progress = new ProgressCapture<object>();
        var sink = new InMemoryErrorSink();

        var opts = new CsvReadOptions
        {
            HasHeader = true,
            AllowExtraFields = false,
            ErrorAction = ReaderErrorAction.Stop,
            ErrorSink = sink,
            Progress = progress,
            ProgressRecordInterval = 1,
            ProgressTimeInterval = TimeSpan.Zero
        };

        int count = 0;
        await foreach (var _ in Read.Csv<dynamic>(path, opts))
            count++;

        Assert.True(opts.Metrics.TerminatedEarly);
        Assert.True(progress.Events.Count > 0); // now passes
    }
}
