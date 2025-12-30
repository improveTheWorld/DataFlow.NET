using System.Text;
using System.Text.Json;
using DataFlow.Data.Tests.Utilities;

namespace DataFlow.Data.Tests.Generators;

public static class DataSetGenerator
{
    public sealed record GeneratedFiles(
        string CsvPath,
        string CsvHeaderlessPath,
        string JsonArrayPath,
        string JsonSingleObjectPath,
        string YamlSequencePath,
        string YamlMultiDocPath,
        string TextPath);

    public static void EnsureVariants(string baseRoot,
                                  out GeneratedFiles clean,
                                  out GeneratedFiles noisy)
    {
        var cleanCfg = new DataGenConfig { InjectErrors = false };
        var noisyCfg = new DataGenConfig(); // default true
        clean = EnsureGenerated(Path.Combine(baseRoot, "clean"), cleanCfg);
        noisy = EnsureGenerated(Path.Combine(baseRoot, "noisy"), noisyCfg);
    }
    public static GeneratedFiles EnsureGenerated(string root, DataGenConfig config, Action<string>? logger = null)
    {
        Directory.CreateDirectory(root);
        var csv = Path.Combine(root, "data.csv");
        var csvNoHeader = Path.Combine(root, "data_no_header.csv");
        var json = Path.Combine(root, "data.json");
        var jsonSingle = Path.Combine(root, "single.json");
        var yamlSeq = Path.Combine(root, "config_seq.yaml");
        var yamlMulti = Path.Combine(root, "config_multi.yaml");
        var text = Path.Combine(root, "lines.txt");

        if (!File.Exists(csv)) GenerateCsv(csv, config, includeHeader: true, logger);
        if (!File.Exists(csvNoHeader)) GenerateCsv(csvNoHeader, config, includeHeader: false, logger);
        if (!File.Exists(json)) GenerateJsonArray(json, config, logger);
        if (config.JsonIncludeSingleObject && !File.Exists(jsonSingle)) GenerateJsonSingle(jsonSingle, logger);
        if (!File.Exists(yamlSeq)) GenerateYamlSequence(yamlSeq, config, logger);
        if (!File.Exists(yamlMulti)) GenerateYamlMultiDoc(yamlMulti, config, logger);
        if (!File.Exists(text)) GenerateText(text, config, logger);

        logger?.Invoke("Dataset generation complete.");
        return new GeneratedFiles(csv, csvNoHeader, json, jsonSingle, yamlSeq, yamlMulti, text);
    }

    private static void GenerateCsv(string path, DataGenConfig cfg, bool includeHeader, Action<string>? log)
    {
        log?.Invoke($"Generating CSV {(includeHeader ? "with" : "no")} header: {path}");
        var rnd = new Random(cfg.Seed + (includeHeader ? 17 : 29));
        using var sw = new StreamWriter(path, false, new UTF8Encoding(false));

        if (includeHeader)
        {
            var header = string.Join(",", Enumerable.Range(1, cfg.CsvColumns).Select(i => $"Col{i}"));
            sw.WriteLine(header);
        }

        for (int r = 0; r < cfg.CsvRows; r++)
        {
            var fields = new string[cfg.CsvColumns];
            for (int c = 0; c < cfg.CsvColumns; c++)
            {
                string baseVal = (rnd.Next(0, 1_000_000)).ToString();
                // Leading zero injection
                if (rnd.NextDouble() < cfg.CsvLeadingZeroRate)
                    baseVal = "0" + (rnd.Next(0, 99999)).ToString("00000");
                // Large integer
                if (rnd.NextDouble() < cfg.CsvLargeIntegerRate)
                    baseVal = new string('9', 19);
                // Possible multi-line
                if (rnd.NextDouble() < cfg.CsvMultilineRate)
                    baseVal = $"\"Line1-{r}-{c}\r\nLine2-{r}-{c}\"";
                // Stray quote error injection (only when InjectErrors is enabled)
                else if (cfg.InjectErrors && rnd.NextDouble() < cfg.CsvStrayQuoteRate)
                    baseVal = $"Bad\"Quote{r}-{c}";
                else if (cfg.InjectErrors && rnd.NextDouble() < cfg.CsvTrailingGarbageRate)
                    baseVal = $"\"OK{r}-{c}\"XYZ";

                // Wrap some random plain strings also with quotes to exercise quotes
                if (!baseVal.StartsWith("\"") && rnd.NextDouble() < 0.05)
                {
                    var v = $"Q{r}_{c}";
                    baseVal = $"\"{v.Replace("\"", "\"\"")}\"";
                }

                fields[c] = baseVal;
            }
            sw.WriteLine(string.Join(",", fields));
        }
    }

    private static void GenerateJsonArray(string path, DataGenConfig cfg, Action<string>? log)
    {
        log?.Invoke($"Generating JSON array: {path}");
        var rnd = new Random(cfg.Seed + 101);
        using var sw = new StreamWriter(path, false, new UTF8Encoding(false));
        sw.Write("[");
        for (int i = 0; i < cfg.JsonArrayLength; i++)
        {
            if (i > 0) sw.Write(",");
            bool emitSemanticallyInvalid = cfg.InjectErrors && rnd.NextDouble() < cfg.JsonInvalidElementRate;

            if (emitSemanticallyInvalid)
            {
                // Missing 'amount' (and other fields) -> semantically invalid for validator, but JSON is valid
                sw.Write(JsonSerializer.Serialize(new
                {
                    id = i,
                    ok = (i % 3 == 0),
                    code = (i % 2 == 0) ? $"EVT-{i}" : null
                }));
            }
            else
            {
                sw.Write(JsonSerializer.Serialize(new
                {
                    id = i,
                    code = (i % 2 == 0) ? $"EVT-{i}" : null,
                    amount = Math.Round(rnd.NextDouble() * 10000, 2),
                    ok = (i % 3 == 0),
                    when = DateTime.UtcNow.AddSeconds(-i).ToString("O"),
                    guid = Guid.NewGuid(),
                    nested = new { a = i % 7, b = $"X{i % 10}" }
                }));
            }

        }
        sw.Write("]");
    }


    private static void GenerateJsonSingle(string path, Action<string>? log)
    {
        log?.Invoke($"Generating JSON single object: {path}");
        var single = new
        {
            id = 1,
            code = "SINGLE",
            amount = 123.45,
            ok = true,
            when = DateTime.UtcNow.ToString("O"),
            guid = Guid.NewGuid()
        };
        File.WriteAllText(path, JsonSerializer.Serialize(single));
    }

    private static void GenerateYamlSequence(string path, DataGenConfig cfg, Action<string>? log)
    {
        log?.Invoke($"Generating YAML sequence: {path}");
        var rnd = new Random(cfg.Seed + 700);
        using var sw = new StreamWriter(path, false, new UTF8Encoding(false));
        sw.WriteLine("-");
        for (int i = 0; i < cfg.YamlDocuments; i++)
        {
            // Sequence root variant
            var alias = (rnd.NextDouble() < cfg.YamlAliasRate) ? "&anchor" : null;
            var line = $"  id: {i}\n  name: item_{i}\n  ok: {(i % 2 == 0).ToString().ToLower()}";
            if (alias != null)
                line = line.Insert(0, $"  {alias}\n");
            sw.WriteLine(line);
            if (rnd.NextDouble() < 0.003) sw.WriteLine("  desc: |\n    multi-line\n    block");
            if (rnd.NextDouble() < cfg.YamlCustomTagRate)
                sw.WriteLine("  custom: !mytag something");
            if (i < cfg.YamlDocuments - 1) sw.WriteLine("-");
        }
    }

    private static void GenerateYamlMultiDoc(string path, DataGenConfig cfg, Action<string>? log)
    {
        log?.Invoke($"Generating YAML multi-doc: {path}");
        using var sw = new StreamWriter(path, false, new UTF8Encoding(false));
        for (int i = 0; i < Math.Min(cfg.YamlDocuments, 100); i++) // smaller
        {
            sw.WriteLine("---");
            sw.WriteLine($"id: {i}");
            sw.WriteLine($"name: multi_{i}");
            sw.WriteLine($"ok: {(i % 2 == 0).ToString().ToLower()}");
        }
    }

    private static void GenerateText(string path, DataGenConfig cfg, Action<string>? log)
    {
        log?.Invoke($"Generating text lines: {path}");
        var rnd = new Random(cfg.Seed + 300);
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ";
        using var sw = new StreamWriter(path, false, new UTF8Encoding(false));
        for (int i = 0; i < cfg.TextLines; i++)
        {
            int len = (int)Math.Max(5, rnd.NextGaussian(cfg.TextAvgLength, cfg.TextAvgLength / 3.0));
            var sb = new StringBuilder(len);
            for (int k = 0; k < len; k++)
                sb.Append(chars[rnd.Next(chars.Length)]);
            sw.WriteLine(sb.ToString().TrimEnd());
        }
    }

    // Simple Gaussian approximation for variability
    private static double NextGaussian(this Random rnd, double mean, double stddev)
    {
        // Box-Muller
        var u1 = 1.0 - rnd.NextDouble();
        var u2 = 1.0 - rnd.NextDouble();
        var randStdNormal =
            Math.Sqrt(-2.0 * Math.Log(u1)) *
            Math.Sin(2.0 * Math.PI * u2);
        return mean + stddev * randStdNormal;
    }
}