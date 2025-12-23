using System.Text.Json;
using System.Text.Json.Serialization;
using DataFlow.Data;
using Xunit;

namespace DataFlow.Data.Tests.Json;

public class JsonCustomConverterTests
{
    public sealed record Money(decimal Amount, string Currency);

    public sealed class MoneyConverter : JsonConverter<Money>
    {
        public override Money? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString()!;
                // Expect format CUR:amount e.g., USD:12.34
                var parts = s.Split(':');
                return new Money(decimal.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), parts[0]);
            }
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                var root = doc.RootElement;
                return new Money(root.GetProperty("amount").GetDecimal(), root.GetProperty("currency").GetString()!);
            }
            throw new JsonException("Unexpected token for Money");
        }

        public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
            => writer.WriteStringValue($"{value.Currency}:{value.Amount}");
    }

    public sealed record Order(int id, Money price);

    [Fact]
    public async Task Custom_Json_Converter_Deserializes()
    {
        var path = Path.GetTempFileName();
    File.WriteAllText(path, "[{\"id\":1,\"price\":\"USD:19.99\"},{\"id\":2,\"price\":{\"amount\":5.50,\"currency\":\"EUR\"}}]");

        var opts = new JsonReadOptions<Order>
        {
            RequireArrayRoot = true,
            SerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new MoneyConverter() }
            }
        };

    var list = new List<Order>();
    await foreach (var o in Read.Json<Order>(path, opts))
            list.Add(o);

        Assert.Equal(2, list.Count);
        Assert.Equal(19.99m, list[0].price.Amount);
        Assert.Equal("USD", list[0].price.Currency);
        Assert.Equal("EUR", list[1].price.Currency);
        Assert.Equal(5.50m, list[1].price.Amount);
    }
}
