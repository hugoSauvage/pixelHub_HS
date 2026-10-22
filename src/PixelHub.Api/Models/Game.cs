using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace PixelHub.Api.Models;

[BsonIgnoreExtraElements]
public class Game
{
    [BsonId]
    [JsonConverter(typeof(ObjectIdJsonConverter))]
    public ObjectId Id { get; set; }

    [BsonElement("titre")]
    public string Titre { get; set; } = "";

    [BsonElement("genre")]
    public string Genre { get; set; } = "";

    [BsonElement("note")]
    public double Note { get; set; }

    [BsonElement("anneeSortie")]
    public int AnneeSortie { get; set; }

    [BsonElement("plateformes")]
    public List<string> Plateformes { get; set; } = new();

    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();

    // Séance 4 bis : les documents sans prix correspondent à des jeux gratuits.
    [BsonElement("prix")]
    public int Prix { get; set; }
}