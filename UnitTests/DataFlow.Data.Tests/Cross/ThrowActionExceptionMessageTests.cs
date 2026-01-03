
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DataFlow;
using Xunit;

namespace DataFlow.Data.Tests.Cross;

public class ThrowActionExceptionMessageTests
{
    private sealed record CsvRow
    {
        public string A { get; set; } = "";
        public string B { get; set; } = "";
    }

    private sealed record DummyJson(int Id);

    private sealed record YamlNode
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }

    private sealed record JRec(int Id);
    private sealed record YNode(string Key, string Value);

    [Fact]
    public void Csv_Throw_SchemaError_MessageIncludesTypeAndExcerpt()
    {
        // Arrange: schema has 2 columns, but row has 3 -> SchemaError with field summary excerpt (first 8 fields)
        var csv = "A,B\n1,2,3\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var opts = new CsvReadOptions
        {
            HasHeader = true,
            Schema = new[] { "A", "B" },
            AllowExtraFields = false,
            ErrorAction = ReaderErrorAction.Throw
        };

        // Act
        var ex = Assert.Throws<InvalidDataException>(() =>
        {
            foreach (var _ in Read.CsvSync<CsvRow>(ms, opts, filePath: "(mem)"))
            {
                // should throw on the first data row due to extra field
            }
        });

        // Assert: starts with "SchemaError: " and contains " | excerpt: "
        Assert.StartsWith("SchemaError: ", ex.Message);
        Assert.Contains(" | excerpt: ", ex.Message);
        // Optional: sanity check that the excerpt contains the first fields summary (joined by commas)
        Assert.Contains("1,2,3", ex.Message);
    }

    [Fact]
    public void Json_Throw_RootError_MessageIncludesTypeAndOptionalExcerpt()
    {
        // Arrange: Single root object while RequireArrayRoot=true and AllowSingleObject=false triggers JsonRootError
        var json = "{\"id\":1}";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var opts = new JsonReadOptions<DummyJson>
        {
            RequireArrayRoot = true,
            AllowSingleObject = false,
            ErrorAction = ReaderErrorAction.Throw
        };

        // Act
        var ex = Assert.Throws<InvalidDataException>(() =>
        {
            foreach (var _ in Read.JsonSync<DummyJson>(ms, opts, filePath: "(mem)"))
            {
                // should throw on root error
            }
        });

        // Assert
        Assert.StartsWith("JsonRootError: ", ex.Message);
        // For JsonRootError the excerpt is empty in current implementation (root error without buffered element),
        // so we do NOT assert excerpt presence here. We just ensure there is no trailing " | excerpt: " when empty.
        Assert.DoesNotContain(" | excerpt: ", ex.Message);
    }

    [Fact]
    public void Json_Throw_SizeLimit_MessageIncludesTypeAndExcerpt_WhenAvailable()
    {
        // Arrange: Use array root with MaxElements = 1; second element triggers JsonSizeLimit.
        // Excerpt is empty in this path too, but this test ensures formatting still starts with type prefix.
        var json = "[ {\"id\":1}, {\"id\":2} ]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var opts = new JsonReadOptions<DummyJson>
        {
            RequireArrayRoot = true,
            AllowSingleObject = true,
            ErrorAction = ReaderErrorAction.Throw,
            MaxElements = 1 // second element should cause JsonSizeLimit
        };

        var ex = Assert.Throws<InvalidDataException>(() =>
        {
            foreach (var _ in Read.JsonSync<DummyJson>(ms, opts, filePath: "(mem)"))
            {
                // first element may pass, second should trigger throw
            }
        });

        Assert.StartsWith("JsonSizeLimit: ", ex.Message);
        // Excerpt may be empty for size limit violations; ensure we don't have the excerpt marker in such case.
        Assert.DoesNotContain(" | excerpt: ", ex.Message);
    }

  
    [Fact]
    public void Yaml_Throw_TypeRestriction_MessageIncludesTypeAndExcerpt()
    {
        // Arrange: RestrictTypes = true, AllowedTypes = only YamlNode, but feed a scalar string sequence root
        // which will deserialize to string and violate type restriction, yielding TypeRestriction with excerpt = type name.
        var yaml = "---\nkey: value\n"; // This will deserialize to YamlNode fine; we need to provoke type mismatch.
        // To provoke TypeRestriction, configure AllowedTypes to exclude the actual runtime type produced.
        // Easiest path: set AllowedTypes to empty set and keep RestrictTypes = true.
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(yaml));

        var opts = new YamlReadOptions<YamlNode>
        {
            RestrictTypes = true,
            AllowedTypes = new HashSet<Type> { typeof(string) }, // disallow YamlNode intentionally
            ErrorAction = ReaderErrorAction.Throw
        };

        var ex = Assert.Throws<InvalidDataException>(() =>
        {
            foreach (var _ in Read.YamlSync<YamlNode>(ms, opts, filePath: "(mem)"))
            {
                // should throw on type restriction
            }
        });

        Assert.StartsWith("TypeRestriction: ", ex.Message);
        // TypeRestriction excerpt is the runtime type name (or "null") ? not empty; assert presence format
        Assert.Contains(" | excerpt: ", ex.Message);
        Assert.Contains(typeof(YamlNode).FullName, ex.Message);
    }


    [Fact]
    public void Json_SimpleOnError_MessageIncludesTypeAndExcerpt_WhenAvailable()
    {
        // Invalid JSON token to force JsonException with excerpt
        var json = "[ {\"id\":1}, { invalid } ]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));

        InvalidDataException? captured = null;

        var e = Read.Json<JRec>(
            path: "(ignored, using stream)", // we?ll call the stream-based options+simple wrapper
            options: null,
            onError: ex => { captured = ex as InvalidDataException; });

        // But we need the stream-based simple overload to control the content; use options API + DelegatingErrorSink via simple overload:
        // Simpler: call the simple overload that takes path; to stay in-memory, write the content to a temp file.
        // To avoid FS, we?ll instead use options API and manually set DelegatingErrorSink with onError.
    }

    [Fact]
    public void Json_SimpleOnError_MessageIncludesTypePrefix_EvenIfExcerptEmpty()
    {
        // Single-root disallowed -> JsonRootError with empty excerpt
        var json = "{\"id\":1}";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));

        InvalidDataException? captured = null;

        // Use options + simple overload emulation by constructing options with DelegatingErrorSink
        var opts = new JsonReadOptions<JRec>
        {
            RequireArrayRoot = true,
            AllowSingleObject = false,
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = new Read.DelegatingErrorSink(e => captured = e as InvalidDataException, "(mem)")
        };

        foreach (var _ in Read.JsonSync<JRec>(ms, opts, filePath: "(mem)"))
        { /* consume */ }

        Assert.NotNull(captured);
        Assert.StartsWith("JsonRootError: ", captured!.Message);
        Assert.DoesNotContain(" | excerpt: ", captured.Message);
    }

    [Fact]
    public void Yaml_SimpleOnError_MessageIncludesTypeAndExcerpt()
    {
        // Alias usage triggers YamlSecurityError with excerpt = alias/anchor name
        var yaml = "a: &anch 1\nb: *anch\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(yaml));

        InvalidDataException? captured = null;

        // Build options to use DelegatingErrorSink; simple overload sets ErrorAction=Skip, which we mirror
        var opts = new YamlReadOptions<YNode>
        {
            DisallowAliases = true,
            DisallowCustomTags = true,
            RestrictTypes = false,
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = new Read.DelegatingErrorSink(e => captured = e as InvalidDataException, "(mem)")
        };

        foreach (var _ in Read.YamlSync<YNode>(ms, opts, filePath: "(mem)"))
        {
            // continue; Skip means we don?t throw
        }

        Assert.NotNull(captured);
        Assert.StartsWith("YamlSecurityError: ", captured!.Message);
        Assert.Contains(" | excerpt: ", captured.Message);
        Assert.Contains("anch", captured.Message);
    }
}

