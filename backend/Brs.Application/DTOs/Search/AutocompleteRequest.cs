namespace Brs.Application.DTOs.Search;

/// <summary>Запрос автодополнения поиска.</summary>
public class AutocompleteRequest
{
    /// <summary>Поисковая строка, минимум 2 символа.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Максимум подсказок (по умолчанию 5, максимум 10).</summary>
    public int Limit { get; set; } = 5;
}
