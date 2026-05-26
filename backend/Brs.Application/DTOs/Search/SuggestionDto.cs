namespace Brs.Application.DTOs.Search;

/// <summary>Одна подсказка автодополнения.</summary>
public class SuggestionDto
{
    public string Text { get; set; } = string.Empty;

    /// <summary>Тип сущности: "book" | "author" | "genre".</summary>
    public string Type { get; set; } = string.Empty;

    public Guid Id { get; set; }
}
