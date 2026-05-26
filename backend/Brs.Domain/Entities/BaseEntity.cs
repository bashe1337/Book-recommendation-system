namespace Brs.Domain.Entities;

/// <summary>
/// Базовый класс для всех доменных сущностей. Содержит идентификатор Guid,
/// что упрощает генерацию ключей на стороне приложения и репликацию данных.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Уникальный идентификатор сущности.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
}
