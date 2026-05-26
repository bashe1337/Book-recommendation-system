// === MOCK DATA — каталог. Источник правды — mocks/library.js ===
// TODO: GET /api/books            — список всех книг с фильтрами и сортировкой
// TODO: GET /api/books/search     — поиск с подсказками (pg_trgm на бэке)
// TODO: GET /api/books/filters    — справочники жанров/авторов/языков

import { BOOKS, ALL_GENRES, ALL_AUTHORS, ALL_LANGUAGES } from './library.js';

export const mockBooks     = BOOKS;
export const mockGenres    = ALL_GENRES;
export const mockAuthors   = ALL_AUTHORS;
export const mockLanguages = ALL_LANGUAGES;

// Границы фильтра по году издания.
export const YEAR_MIN = 1830;
export const YEAR_MAX = 2025;

// Имитация триграммного поиска: substring match по title и author.
// TODO: заменить на GET /api/books/search?q=...&suggest=true
export const getSuggestions = (query) => {
	if (!query || query.length < 2) return [];
	const q = query.toLowerCase();
	return BOOKS
		.filter(
			(b) =>
				b.title.toLowerCase().includes(q) ||
				b.author.toLowerCase().includes(q)
		)
		.slice(0, 8)
		.map((b) => ({
			label: `${b.title} — ${b.author}`,
			id: b.id,
			title: b.title,
			author: b.author
		}));
};
