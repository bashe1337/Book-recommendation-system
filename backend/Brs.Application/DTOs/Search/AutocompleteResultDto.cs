namespace Brs.Application.DTOs.Search;

/// <summary>Результат автодополнения — список подсказок.</summary>
public class AutocompleteResultDto
{
    public List<SuggestionDto> Suggestions { get; set; } = new();
}
