using System.Text.Json.Serialization;

namespace Brs.Infrastructure.ExternalApis.GoogleBooks;

/// <summary>Корневой ответ Google Books volumes API.</summary>
public class GoogleBooksResponse
{
    [JsonPropertyName("items")]
    public List<GoogleBooksVolume>? Items { get; set; }
}

/// <summary>Один том (книга) в ответе Google Books.</summary>
public class GoogleBooksVolume
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("volumeInfo")]
    public GoogleBooksVolumeInfo VolumeInfo { get; set; } = new();
}

/// <summary>Метаданные тома.</summary>
public class GoogleBooksVolumeInfo
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("authors")]
    public List<string>? Authors { get; set; }

    [JsonPropertyName("publishedDate")]
    public string? PublishedDate { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("imageLinks")]
    public GoogleBooksImageLinks? ImageLinks { get; set; }

    [JsonPropertyName("industryIdentifiers")]
    public List<GoogleBooksIdentifier>? IndustryIdentifiers { get; set; }
}

/// <summary>Ссылки на обложки разного размера.</summary>
public class GoogleBooksImageLinks
{
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("smallThumbnail")]
    public string? SmallThumbnail { get; set; }
}

/// <summary>Отраслевой идентификатор (ISBN_10, ISBN_13 и т.п.).</summary>
public class GoogleBooksIdentifier
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }
}
