namespace Brs.Domain.Entities;

/// <summary>Автор книги.</summary>
public class Author : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Bio { get; set; }

    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
}
