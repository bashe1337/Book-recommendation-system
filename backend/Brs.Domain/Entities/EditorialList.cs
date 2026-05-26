namespace Brs.Domain.Entities;

/// <summary>Редакционная подборка книг (составляется администраторами).</summary>
public class EditorialList : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // причина: many-to-many через простую коллекцию книг —
    // EF создаст таблицу-связку автоматически (подборки редко меняются,
    // дополнительных полей в связке не требуется)
    public ICollection<Book> Books { get; set; } = new List<Book>();
}
