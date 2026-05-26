namespace Brs.Domain.Entities;

/// <summary>Связующая сущность «книга — тег» (many-to-many).</summary>
public class BookTag
{
    public Guid BookId { get; set; }
    public Guid TagId { get; set; }

    public Book? Book { get; set; }
    public Tag? Tag { get; set; }
}
