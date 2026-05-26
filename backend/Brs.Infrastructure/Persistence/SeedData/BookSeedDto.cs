using System.Text.Json.Serialization;

namespace Brs.Infrastructure.Persistence.SeedData;

/// <summary>POCO для чтения books.json (формат совпадает с frontend mocks/library.js).</summary>
internal class BookSeedDto
{
    [JsonPropertyName("id")] public int MockId { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("author")] public string Author { get; set; } = string.Empty;
    [JsonPropertyName("genres")] public List<string> Genres { get; set; } = new();
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = new();
    [JsonPropertyName("year")] public int Year { get; set; }
    [JsonPropertyName("lang")] public string Lang { get; set; } = string.Empty;
    [JsonPropertyName("rating")] public double Rating { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("series")] public string? Series { get; set; }
    [JsonPropertyName("seriesIndex")] public int? SeriesIndex { get; set; }
}
