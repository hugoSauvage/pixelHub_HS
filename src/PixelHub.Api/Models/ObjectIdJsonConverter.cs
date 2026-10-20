using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;

namespace PixelHub.Api.Models;

public class ObjectIdJsonConverter : JsonConverter<ObjectId>
{
    public override ObjectId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        ObjectId.Parse(reader.GetString()
            ?? throw new JsonException("Un ObjectId doit être représenté par une chaîne."));

    public override void Write(
        Utf8JsonWriter writer,
        ObjectId value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
