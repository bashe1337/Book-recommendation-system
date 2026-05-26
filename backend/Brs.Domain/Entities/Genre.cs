namespace Brs.Domain.Entities;

/// <summary>Жанр книги (Фэнтези, Детектив и т. д.).</summary>
public class Genre : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    // причина: slug используется в URL и для быстрых lookup по человекочитаемому ключу
    public string Slug { get; set; } = string.Empty;

    public ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
}
