// Мост между DTO бэкенда и пропсами наших карточек.
//
// BookSummaryDto на бэке:
//   { id, title, coverUrl, avgRating, ratingCount, authors: string[], genres: string[] }
//
// BookDto (полная карточка) — то же + description, publishedYear, language, isbn,
// плюс authors: AuthorDto[], genres: GenreDto[], tags: TagDto[].
//
// BookCard ждёт:
//   { id, title, author | authors[], cover, rating, year?, genres?, tags? }
//
// Эти хелперы делают преобразование в одном месте, чтобы при правках API
// не пришлось чинить всё дерево компонентов.

/**
 * Преобразует BookSummaryDto в формат BookCard.
 * Принимает обе формы (camelCase из JSON и PascalCase на всякий случай).
 */
export const mapBookSummary = (dto) => {
	if (!dto) return null;
	const id = dto.id ?? dto.Id;
	const title = dto.title ?? dto.Title ?? '';
	const cover = dto.coverUrl ?? dto.CoverUrl ?? null;
	const rating = dto.avgRating ?? dto.AvgRating ?? 0;
	const ratingCount = dto.ratingCount ?? dto.RatingCount ?? 0;
	const authors = dto.authors ?? dto.Authors ?? [];
	const genres = dto.genres ?? dto.Genres ?? [];
	return {
		id,
		title,
		author: Array.isArray(authors) && authors.length ? authors.join(', ') : '',
		authors: Array.isArray(authors) ? authors : [],
		cover,
		rating,
		ratingCount,
		genres
	};
};

/** Преобразует BookDto (полная карточка книги) в форму для страницы /book/:id. */
export const mapBook = (dto) => {
	if (!dto) return null;
	const authors = (dto.authors ?? dto.Authors ?? []).map((a) => a.name ?? a.Name ?? a);
	const genres = (dto.genres ?? dto.Genres ?? []).map((g) => g.name ?? g.Name ?? g);
	const tags = (dto.tags ?? dto.Tags ?? []).map((t) => t.name ?? t.Name ?? t);

	return {
		id:          dto.id ?? dto.Id,
		title:       dto.title ?? dto.Title ?? '',
		description: dto.description ?? dto.Description ?? '',
		year:        dto.publishedYear ?? dto.PublishedYear ?? null,
		lang:        dto.language ?? dto.Language ?? '',
		isbn:        dto.isbn ?? dto.ISBN ?? '',
		cover:       dto.coverUrl ?? dto.CoverUrl ?? null,
		rating:      dto.avgRating ?? dto.AvgRating ?? 0,
		ratingCount: dto.ratingCount ?? dto.RatingCount ?? 0,
		authors,
		author: authors.join(', '),
		genres,
		tags
	};
};

/** Список BookSummaryDto → массив карточек. */
export const mapBookList = (list) =>
	(Array.isArray(list) ? list : []).map(mapBookSummary).filter(Boolean);
