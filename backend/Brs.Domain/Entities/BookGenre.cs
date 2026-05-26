namespace Brs.Domain.Entities;

/// <summary>Связующая сущность «книга — жанр» (many-to-many).</summary>
public class BookGenre
{
    public Guid BookId { get; set; }
    public Guid GenreId { get; set; }

    public Book? Book { get; set; }
    public Genre? Genre { get; set; }
}
