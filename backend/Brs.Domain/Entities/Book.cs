using NpgsqlTypes;

namespace Brs.Domain.Entities;

/// <summary>Книга — основная сущность каталога.</summary>
public class Book : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PublishedYear { get; set; }
    public string? Language { get; set; }
    public string? ISBN { get; set; }

    // причина: внешний идентификатор Google Books позволяет дедуплицировать
    // импорт и подтягивать обновлённые метаданные
    public string? GoogleBooksId { get; set; }

    public string? CoverUrl { get; set; }

    // причина: агрегаты рейтинга денормализованы для быстрых выборок без JOIN
    public double AvgRating { get; set; }
    public int RatingCount { get; set; }

    /// <summary>
    /// Вектор полнотекстового поиска. Заполняется триггером в БД,
    /// EF только читает значение — попытка вставки приведёт к ошибке.
    /// причина типа: Npgsql не умеет мапить string → tsvector; используем
    /// NpgsqlTsVector — нативное представление tsvector в провайдере.
    /// </summary>
    public NpgsqlTsVector? SearchVector { get; private set; }

    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
    public ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
    public ICollection<BookTag> BookTags { get; set; } = new List<BookTag>();
}
