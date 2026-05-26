namespace Brs.Application.DTOs.Books;

/// <summary>Представление автора.</summary>
public class AuthorDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Bio { get; set; }
}
