using System.ComponentModel.DataAnnotations;

namespace Brs.Application.DTOs.Interactions;

/// <summary>Запрос выставления оценки книге.</summary>
public class RateBookRequest
{
    [Range(1, 5, ErrorMessage = "Оценка должна быть от 1 до 5.")]
    public int Score { get; set; }
}
