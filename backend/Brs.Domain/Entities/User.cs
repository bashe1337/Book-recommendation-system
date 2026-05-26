namespace Brs.Domain.Entities;

/// <summary>Роль пользователя в системе.</summary>
public enum UserRole
{
    /// <summary>Обычный пользователь сервиса.</summary>
    User = 0,
    /// <summary>Администратор: модерация контента, управление подборками.</summary>
    Admin = 1
}

/// <summary>Зарегистрированный пользователь сервиса рекомендаций.</summary>
public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;

    // причина: храним только хеш — пароль в открытом виде в БД недопустим
    public string PasswordHash { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public UserRole Role { get; set; } = UserRole.User;

    // причина: предпочтения по жанрам нужны для холодного старта рекомендаций;
    // храним как массив, чтобы избежать лишней таблицы-связки при простой выборке
    public List<Guid> PreferredGenreIds { get; set; } = new();

    // причина: храним refresh token прямо в строке пользователя — простой и
    // достаточный механизм для одной активной сессии на пользователя.
    // TODO: вынести в отдельную таблицу при необходимости поддержать несколько сессий
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<ViewHistory> ViewHistory { get; set; } = new List<ViewHistory>();
}
