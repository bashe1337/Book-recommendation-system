// === MOCK DATA — страница книги. Источник правды — mocks/library.js ===
//
// TODO: GET /api/books/:id                     — данные книги
// TODO: GET /api/books/:id/rating              — агрегированный рейтинг + распределение
// TODO: GET /api/reviews?bookId=:id            — список отзывов
// TODO: POST /api/reviews                      — публикация отзыва
// TODO: POST /api/favorites                    — добавить в избранное
// TODO: POST /api/reading-history              — отметить прочитанным
// TODO: GET /api/recommendations/similar/:id   — похожие книги (item-based)

import { BOOKS, findBook, bySeries } from './library.js';

// «mockBooks» — словарь книг по id для совместимости со старой страницей.
// Любая книга из библиотеки теперь открывается по /book/:id.
export const mockBooks = BOOKS.reduce((acc, b) => {
	acc[b.id] = {
		id: b.id,
		title: b.title,
		// На странице книги используется массив authors[].
		authors: [b.author],
		genres: b.genres,
		tags: b.tags,
		year: b.year,
		lang: b.lang,
		isbn: generateIsbn(b.id),
		cover: b.cover, // null → SVG-заглушка
		description: b.description,
		series: b.series,
		seriesIndex: b.seriesIndex
	};
	return acc;
}, {});

// ISBN детерминированно генерируем по id, чтобы поле выглядело реалистично.
function generateIsbn(id) {
	const a = String(900 + (id % 100)).padStart(3, '0');
	const b = String(1000 + id * 13).slice(-4);
	const c = String(id * 7919).slice(-5).padStart(5, '0');
	return `978-5-${a}-${b}-${c.slice(0, 1)}`;
}

// Общий блок агрегированного рейтинга и распределения.
// distribution — проценты по звёздам, сумма ≈ 100.
export const mockRating = {
	average: 4.8,
	count: 1247,
	distribution: { 5: 68, 4: 20, 3: 7, 2: 3, 1: 2 }
};

export const mockReviews = [
	{ id: 1, user: 'Анна К.',    avatar: null, date: '2025-03-15', rating: 5, sentiment: 'positive', likes: 24, text: 'Потрясающая книга! Читала за один день, оторваться невозможно. Булгаков — гений.' },
	{ id: 2, user: 'Игорь М.',   avatar: null, date: '2025-02-28', rating: 4, sentiment: 'positive', likes: 11, text: 'Очень глубокое произведение. Местами сложно, но это того стоит.' },
	{ id: 3, user: 'Светлана Р.',avatar: null, date: '2025-01-10', rating: 3, sentiment: 'neutral',  likes: 3,  text: 'Книга хорошая, но я ожидала чего-то другого. Читать стоит, но не шедевр.' },
	{ id: 4, user: 'Дмитрий В.', avatar: null, date: '2024-12-05', rating: 2, sentiment: 'negative', likes: 1,  text: 'Не понял смысла. Слишком много символизма, который мне не близок.' },
	{ id: 5, user: 'Мария Л.',   avatar: null, date: '2024-11-20', rating: 5, sentiment: 'positive', likes: 37, text: 'Перечитываю уже в третий раз — каждый раз нахожу что-то новое.' },
	{ id: 6, user: 'Алексей Н.', avatar: null, date: '2024-10-14', rating: 4, sentiment: 'positive', likes: 8,  text: 'Сатира на советскую действительность великолепна. Рекомендую всем.' },
	{ id: 7, user: 'Екатерина С.',avatar: null,date: '2024-09-02', rating: 3, sentiment: 'neutral',  likes: 5,  text: 'Понравились отдельные сцены, но финал показался скомканным.' },
	{ id: 8, user: 'Павел Т.',   avatar: null, date: '2024-08-18', rating: 1, sentiment: 'negative', likes: 0,  text: 'Не моё. Слог тяжёлый, сюжет рваный. Бросил на середине.' }
];

// Похожие книги — берутся не статически, а из той же серии (если она есть)
// плюс книги того же автора. Это даёт реалистичное наполнение блока
// «С этим также читают» на любой странице книги.
export const getSimilarBooks = (currentBookId) => {
	const current = findBook(currentBookId);
	if (!current) return [];

	const seen = new Set([current.id]);
	const result = [];

	// 1) Книги той же серии.
	if (current.series) {
		for (const b of bySeries(current.series)) {
			if (!seen.has(b.id)) { seen.add(b.id); result.push(b); }
		}
	}
	// 2) Книги того же автора.
	for (const b of BOOKS.filter((x) => x.author === current.author)) {
		if (!seen.has(b.id)) { seen.add(b.id); result.push(b); }
	}
	// 3) Добиваем книгами с пересечением жанров.
	for (const b of BOOKS) {
		if (result.length >= 5) break;
		if (seen.has(b.id)) continue;
		if (b.genres.some((g) => current.genres.includes(g))) {
			seen.add(b.id);
			result.push(b);
		}
	}

	// Приводим к компактному виду — карточка BookCardCompact ожидает
	// author (строкой), а не authors[].
	return result.slice(0, 5).map((b) => ({
		id: b.id, title: b.title, author: b.author, rating: b.rating, cover: b.cover
	}));
};

// Для обратной совместимости: страница книги импортировала
// mockSimilarBooks как массив. Оставляем дефолтный набор для книги №43.
export const mockSimilarBooks = getSimilarBooks(43);
