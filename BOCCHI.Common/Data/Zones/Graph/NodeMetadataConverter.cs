using System.Text.Json;
using System.Text.Json.Serialization;

namespace BOCCHI.Common.Data.Zones.Graph;

public class NodeMetadataConverter : JsonConverter<INodeMetadata>
{
    public override INodeMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("MetadataType", out var typeProp))
        {
            throw new JsonException("Missing MetadataType discriminator");
        }

        var typeName = typeProp.GetString();

        return typeName switch
        {
            nameof(BlankNodeMetadata) => JsonSerializer.Deserialize<BlankNodeMetadata>(root.GetRawText(), options)!,
            nameof(CarrotNodeMetadata) or "CarrotNodeMetaData" => JsonSerializer.Deserialize<CarrotNodeMetadata>(root.GetRawText(), options)!,
            nameof(RerollPotChestNodeMetadata) or "RerollPotChestNodeMetaData" => JsonSerializer.Deserialize<RerollPotChestNodeMetadata>(root.GetRawText(), options)!,
            nameof(TeleportNodeMetadata) => JsonSerializer.Deserialize<TeleportNodeMetadata>(root.GetRawText(), options)!,
            nameof(ActivityNodeMetadata) => JsonSerializer.Deserialize<ActivityNodeMetadata>(root.GetRawText(), options)!,
            nameof(TreasureNodeMetadata) or "TreasureNodeMetaData" => JsonSerializer.Deserialize<TreasureNodeMetadata>(root.GetRawText(), options)!,
            nameof(PotChestNodeMetadata) or "PotChestNodeMetaData" => JsonSerializer.Deserialize<PotChestNodeMetadata>(root.GetRawText(), options)!,
            _ => throw new JsonException($"Unknown metadata type: {typeName}")
        };
    }

    public override void Write(Utf8JsonWriter writer, INodeMetadata value, JsonSerializerOptions options)
    {
        var typeName = value.GetType().Name;

        var json = JsonSerializer.SerializeToElement(value, value.GetType(), options);

        writer.WriteStartObject();
        writer.WriteString("MetadataType", typeName);

        foreach (var prop in json.EnumerateObject())
        {
            prop.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
