namespace Brs.Domain.Entities;

/// <summary>Произвольный тег книги (например, «бестселлер», «экранизировано»).</summary>
public class Tag : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public ICollection<BookTag> BookTags { get; set; } = new List<BookTag>();
}
