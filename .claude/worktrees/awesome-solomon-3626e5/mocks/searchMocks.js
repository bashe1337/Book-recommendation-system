// === MOCK DATA — поиск. Источник правды — mocks/library.js ===
//
// TODO: GET /api/books/search?q=...&sort=relevance|rating|year
// TODO: GET /api/books/suggest?q=...
// TODO: GET /api/books/expand?q=...
//
// Имитируем pg_trgm: substring + fuzzy на 1–2 опечатки.

import { BOOKS } from './library.js';

export const mockBooks = BOOKS;

// Популярные запросы для начального экрана (когда нет ?q=).
export const mockPopularQueries = [
	'Булгаков', 'Достоевский', 'Толкин', 'Фантастика',
	'Детектив', 'Классика', 'Толстой', 'Оруэлл', 'Фэнтези'
];

// Словарь синонимов/расширений запросов. На бэке — словарь синонимов + word2vec.
export const mockQueryExpansions = {
	'булгаков':   ['Мастер и Маргарита', 'Собачье сердце', 'Белая гвардия', 'мистика'],
	'фантастика': ['Дюна', 'Солярис', 'Гиперион', '1984', 'антиутопия'],
	'детектив':   ['Шерлок Холмс', 'Агата Кристи', 'Десять негритят', 'криминал'],
	'толстой':    ['Война и мир', 'Анна Каренина', 'классика', 'русская литература'],
	'толкин':     ['Властелин колец', 'Хоббит', 'эпическое фэнтези', 'Средиземье']
};

// Имитация fuzzy (≤2 опечатки).
// TODO: на бэкенде заменено на pg_trgm similarity >= 0.3
export const fuzzyMatch = (str, query) => {
	if (!str || !query) return false;
	if (Math.abs(str.length - query.length) > 3) return false;
	let diff = 0;
	for (let i = 0; i < Math.min(str.length, query.length); i++) {
		if (str[i] !== query[i]) diff++;
		if (diff > 2) return false;
	}
	return diff <= 2;
};

// Подсказки автокомплита: до 8 элементов, помеченных типом совпадения.
export const getSuggestions = (query) => {
	if (!query || query.length < 2) return [];
	const q = query.toLowerCase().trim();

	return BOOKS
		.filter((b) =>
			b.title.toLowerCase().includes(q) ||
			b.author.toLowerCase().includes(q) ||
			b.genres.some((g) => g.toLowerCase().includes(q)) ||
			fuzzyMatch(b.title.toLowerCase(), q) ||
			fuzzyMatch(b.author.toLowerCase(), q)
		)
		.slice(0, 8)
		.map((b) => {
			let type = 'title';
			if (!b.title.toLowerCase().includes(q)) {
				if (b.author.toLowerCase().includes(q))                                 type = 'author';
				else if (b.genres.some((g) => g.toLowerCase().includes(q)))            type = 'genre';
			}
			return { id: b.id, label: b.title, sublabel: b.author, type };
		});
};

// Основной поиск + сортировка по релевантности.
export const performSearch = (query) => {
	if (!query || query.length < 2) return [];
	const q = query.toLowerCase().trim();

	return BOOKS
		.filter((b) =>
			b.title.toLowerCase().includes(q) ||
			b.author.toLowerCase().includes(q) ||
			b.genres.some((g) => g.toLowerCase().includes(q)) ||
			(b.description || '').toLowerCase().includes(q) ||
			(b.tags || []).some((t) => t.toLowerCase().includes(q)) ||
			fuzzyMatch(b.title.toLowerCase(), q) ||
			fuzzyMatch(b.author.toLowerCase(), q)
		)
		.sort((a, b) => {
			const aExact = a.title.toLowerCase().includes(q) ? 1 : 0;
			const bExact = b.title.toLowerCase().includes(q) ? 1 : 0;
			return bExact - aExact || b.rating - a.rating;
		});
};

// Query expansion.
export const getExpansions = (query, results) => {
	if (!query) return [];
	const q = query.toLowerCase().trim();
	if (mockQueryExpansions[q]) return mockQueryExpansions[q];
	return [...new Set((results || []).flatMap((b) => b.genres))].slice(0, 4);
};

// Подсветка совпадения — рендерить через v-html.
// Экранируем HTML, чтобы избежать XSS, и regex-символы.
export const highlightMatch = (text, query) => {
	if (!text) return '';
	const safe = String(text)
		.replace(/&/g, '&amp;')
		.replace(/</g, '&lt;')
		.replace(/>/g, '&gt;');
	if (!query) return safe;
	const escaped = String(query).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
	const regex = new RegExp(`(${escaped})`, 'gi');
	return safe.replace(regex, '<mark class="search-highlight">$1</mark>');
};
