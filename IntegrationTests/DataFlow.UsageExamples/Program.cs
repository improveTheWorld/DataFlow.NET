using App.UsageExamples.legacyExamples;

namespace App.UsageExamples;

/// <summary>
/// Interactive examples from DataFlow.NET documentation
/// Run this project to explore working examples of framework features
/// </summary>
internal class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Support non-interactive mode for CI/automation
        if (args.Length > 0 && args[0].ToLower() == "--all")
        {
            await RunAllExamples();
            return;
        }

        while (true)
        {
            ShowMenu();
            var choice = Console.ReadLine()?.Trim();

            if (choice == "0" || choice?.ToLower() == "q")
                break;

            Console.Clear();
            try
            {
                await RunExample(choice);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
            }

            Console.WriteLine("\n--- Press any key to continue ---");
            Console.ReadKey();
            Console.Clear();
        }

        Console.WriteLine("\nGoodbye! 👋");
    }

    static void ShowMenu()
    {
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════╗
║           DataFlow.NET - Documentation Examples                 ║
╠══════════════════════════════════════════════════════════════════╣
║  📚 README EXAMPLES                                              ║
║    1. Cases Pattern - Batch Processing                           ║
║    2. Quick Start - CSV Processing                               ║
║                                                                  ║
║  🔄 CASES PATTERN (Unified-Processing.md)                        ║
║    3. Configuration-Driven Transformation Tree                   ║
║    4. Supra Category Pattern                                     ║
║    5. Sync/Async Unified Processing                              ║
║                                                                  ║
║  📡 STREAM MERGING (Stream-Merging.md)                           ║
║    6. Basic Stream Merging                                       ║
║    7. Stream Merging with Filtering                              ║
║    8. Stream Merging + Cases Pattern                             ║
║                                                                  ║
║  📖 DATA LAYER (Architecture-APIs.md)                            ║
║    9. Text Reading (Sync)                                        ║
║   10. Text Reading (Async)                                       ║
║   11. CSV Reading                                                ║
║   12. CSV Writing                                                ║
║   13. Utility Extensions                                         ║
║                                                                  ║
║  📚 READING INFRASTRUCTURE (DataFlow-Data-Reading-Infra.md)      ║
║   17. Async/Sync Naming Convention                               ║
║   18. String-Based Parsing                                       ║
║   19. CSV with Error Handling                                    ║
║   20. Raw Text Lines                                             ║
║                                                                  ║
║  🧩 OBJECT MATERIALIZER (ObjectMaterializer.md)                  ║
║   21. Schema-Based Mapping                                       ║
║   22. Order-Based Mapping                                        ║
║   23. Constructor-Based Mapping                                  ║
║   24. Missing/Extra Columns                                      ║
║   25. Bulk Test Data Creation                                    ║
║                                                                  ║
║  🔧 LEGACY EXAMPLES                                              ║
║   14. Original CSV Example                                       ║
║   15. Original Text/Cases Example                                ║
║   16. Original Regex Tokenizer Example                           ║
║                                                                  ║
║  ⚡ PARALLEL QUERIES (Multi-Path Processing)                     ║
║   26. Comprehensive Comparison (4 execution paths)               ║
║   27. Real-time Metrics Monitoring                               ║
║   28. Order Processing with PLINQ                                ║
║   29. IoT Sensor Monitoring                                      ║
║   30. Concurrency Level Comparison                               ║
║                                                                  ║
║   A. Run ALL examples                                            ║
║   0. Exit                                                        ║
╚══════════════════════════════════════════════════════════════════╝
");
        Console.Write("Select an example (1-30, A, or 0): ");
    }

    static async Task RunExample(string? choice)
    {
        switch (choice?.ToUpper())
        {
            // Readme Examples
            case "1":
                await ReadmeExamples.CasesPatternBatchAsync();
                break;
            case "2":
                ReadmeExamples.QuickStartCsvProcessing();
                break;

            // Cases Pattern Examples
            case "3":
                CasesPatternExamples.ConfigurationDrivenTransformationTree();
                break;
            case "4":
                CasesPatternExamples.SupraCategoryPattern();
                break;
            case "5":
                await CasesPatternExamples.SyncAsyncUnifiedAsync();
                break;

            // Stream Merging Examples
            case "6":
                await StreamMergingExamples.BasicStreamMergingAsync();
                break;
            case "7":
                await StreamMergingExamples.StreamMergingWithFilterAsync();
                break;
            case "8":
                await StreamMergingExamples.StreamMergingWithCasesAsync();
                break;

            // Data Layer Examples
            case "9":
                DataLayerExamples.TextReadingSync();
                break;
            case "10":
                await DataLayerExamples.TextReadingAsync();
                break;
            case "11":
                DataLayerExamples.CsvReadingSync();
                break;
            case "12":
                DataLayerExamples.CsvWriting();
                break;
            case "13":
                DataLayerExamples.UtilityExtensions();
                break;

            // Legacy Examples (from original Program.cs)
            case "14":
                LegacyExamples.CsvSimpleExample();
                break;
            case "15":
                LegacyExamples.TextAdvancedExample();
                break;
            case "16":
                LegacyExamples.RgxsUsageExample();
                break;

            // Reading Infrastructure Examples
            case "17":
                await ReadingInfrastructureExamples.AsyncSyncConventionAsync();
                break;
            case "18":
                ReadingInfrastructureExamples.StringBasedParsing();
                break;
            case "19":
                ReadingInfrastructureExamples.SimpleCsvWithErrorHandling();
                break;
            case "20":
                await ReadingInfrastructureExamples.RawTextLinesAsync();
                break;

            // ObjectMaterializer Examples
            case "21":
                ObjectMaterializerExamples.SchemaBasedMapping();
                break;
            case "22":
                ObjectMaterializerExamples.ConstructorBasedMapping();
                break;
            case "23":
                ObjectMaterializerExamples.MissingExtraColumns();
                break;
            case "24":
                ObjectMaterializerExamples.BulkTestDataCreation();
                break;

            // Parallel Queries Examples
            case "26":
                await ParallelQueriesExamples.ComprehensiveComparisonAsync();
                break;
            case "27":
                await ParallelQueriesExamples.MetricsMonitoringAsync();
                break;
            case "28":
                ParallelQueriesExamples.OrderProcessingPlinq();
                break;
            case "29":
                await ParallelQueriesExamples.SensorMonitoringAsync();
                break;
            case "30":
                await ParallelQueriesExamples.ConcurrencyComparisonAsync();
                break;

            // Run All
            case "A":
                await RunAllExamples();
                break;

            default:
                Console.WriteLine("Invalid choice. Please try again.");
                break;
        }
    }

    static async Task RunAllExamples()
    {
        Console.WriteLine("=== Running ALL Examples ===\n");

        var examples = new (string name, Func<Task> action)[]
        {
            ("1. Cases Pattern Batch", ReadmeExamples.CasesPatternBatchAsync),
            ("2. Quick Start CSV", () => { ReadmeExamples.QuickStartCsvProcessing(); return Task.CompletedTask; }),
            ("3. Transformation Tree", () => { CasesPatternExamples.ConfigurationDrivenTransformationTree(); return Task.CompletedTask; }),
            ("4. Supra Category", () => { CasesPatternExamples.SupraCategoryPattern(); return Task.CompletedTask; }),
            ("5. Sync/Async Unified", CasesPatternExamples.SyncAsyncUnifiedAsync),
            ("6. Basic Stream Merging", StreamMergingExamples.BasicStreamMergingAsync),
            ("7. Stream Filter", StreamMergingExamples.StreamMergingWithFilterAsync),
            ("8. Stream + Cases", StreamMergingExamples.StreamMergingWithCasesAsync),
            ("9. Text Sync", () => { DataLayerExamples.TextReadingSync(); return Task.CompletedTask; }),
            ("10. Text Async", DataLayerExamples.TextReadingAsync),
            ("11. CSV Reading", () => { DataLayerExamples.CsvReadingSync(); return Task.CompletedTask; }),
            ("12. CSV Writing", () => { DataLayerExamples.CsvWriting(); return Task.CompletedTask; }),
            ("13. Utility Extensions", () => { DataLayerExamples.UtilityExtensions(); return Task.CompletedTask; }),
        };

        foreach (var (name, action) in examples)
        {
            Console.WriteLine($"\n{'=',-60}");
            Console.WriteLine($"Running: {name}");
            Console.WriteLine($"{'=',-60}\n");
            await action();
            Console.WriteLine();
        }

        Console.WriteLine("\n✅ All examples completed!");
    }
}
