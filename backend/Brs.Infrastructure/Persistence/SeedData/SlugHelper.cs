namespace Brs.Infrastructure.Persistence.SeedData;

/// <summary>Транслитерация русских жанров/языков в URL-friendly slug.</summary>
internal static class SlugHelper
{
    // причина: для известных жанров используем человекочитаемые латинские slug-и,
    // для всего остального — общая транслитерация
    private static readonly Dictionary<string, string> KnownSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Фэнтези"] = "fantasy",
        ["Подростковое"] = "young-adult",
        ["Приключения"] = "adventure",
        ["Эпическое"] = "epic",
        ["Фантастика"] = "sci-fi",
        ["Детектив"] = "detective",
        ["Мистика"] = "mystery",
        ["Классика"] = "classic",
        ["Психологический"] = "psychological",
        ["Философия"] = "philosophy",
        ["Исторический"] = "historical",
        ["Сатира"] = "satire",
        ["Юмор"] = "humor",
        ["Хоррор"] = "horror",
        ["Антиутопия"] = "dystopia",
        ["Драма"] = "drama",
        ["Поэзия"] = "poetry",
        ["Магический реализм"] = "magic-realism"
    };

    private static readonly Dictionary<string, string> LanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Русский"] = "ru",
        ["Английский"] = "en",
        ["Польский"] = "pl",
        ["Немецкий"] = "de",
        ["Испанский"] = "es",
        ["Французский"] = "fr",
        ["Итальянский"] = "it"
    };

    private static readonly Dictionary<char, string> CyrillicMap = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d",
        ['е'] = "e", ['ё'] = "e", ['ж'] = "zh", ['з'] = "z", ['и'] = "i",
        ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
        ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
        ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch",
        ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "",
        ['э'] = "e", ['ю'] = "yu", ['я'] = "ya"
    };

    public static string SlugForGenre(string name)
    {
        if (KnownSlugs.TryGetValue(name.Trim(), out var slug)) return slug;
        return Transliterate(name);
    }

    public static string LanguageCode(string language)
    {
        if (string.IsNullOrWhiteSpace(language)) return string.Empty;
        return LanguageCodes.TryGetValue(language.Trim(), out var code) ? code : language.Trim().ToLowerInvariant();
    }

    private static string Transliterate(string source)
    {
        var result = new System.Text.StringBuilder(source.Length);
        foreach (var ch in source.ToLowerInvariant())
        {
            if (CyrillicMap.TryGetValue(ch, out var mapped)) result.Append(mapped);
            else if (char.IsLetterOrDigit(ch)) result.Append(ch);
            else if (ch is ' ' or '-' or '_') result.Append('-');
        }
        // причина: схлопываем подряд идущие дефисы и обрезаем по краям
        var slug = string.Join('-', result.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length == 0 ? "genre" : slug;
    }
}
