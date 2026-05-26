namespace Brs.Application.DTOs.Recommendations;

/// <summary>Алгоритм рекомендаций, который использовался для ответа.</summary>
public enum RecommendationModel
{
    /// <summary>Content-based (TF-IDF по описаниям/жанрам/тегам).</summary>
    Content = 0,
    /// <summary>SVD (FunkSVD, явные оценки).</summary>
    Svd = 1,
    /// <summary>ALS (implicit feedback: views + favorites + ratings).</summary>
    Als = 2,
    /// <summary>User-based Collaborative Filtering.</summary>
    UserCf = 3,
    /// <summary>Item-based Collaborative Filtering.</summary>
    ItemCf = 4,
    /// <summary>Популярные книги (без ML, по AvgRating × RatingCount).</summary>
    Popularity = 5
}
