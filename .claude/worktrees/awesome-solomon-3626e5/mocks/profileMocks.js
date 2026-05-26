// === MOCK DATA — личный кабинет. Источник правды — mocks/library.js ===
//
// TODO: GET /api/profile/me                   — данные текущего пользователя
// TODO: PUT /api/profile/me                   — обновить профиль
// TODO: POST /api/auth/logout                 — выход
// TODO: GET /api/favorites                    — избранное
// TODO: DELETE /api/favorites/:bookId
// TODO: GET /api/reading-history              — история просмотров
// TODO: DELETE /api/reading-history
// TODO: GET /api/profile/ratings              — оценки пользователя
// TODO: GET /api/profile/reviews              — отзывы пользователя
// TODO: PUT /api/ratings/:bookId
// TODO: GET /api/recommendations/for-you      / by-genres / similar-viewed

import { BOOKS, findBook } from './library.js';

export const mockUser = {
	id: 42,
	name: 'Александр Петров',
	email: 'alex.petrov@example.com',
	avatar: null,
	registeredAt: '2026-03-10',
	preferredGenres: ['Классика', 'Фантастика', 'Фэнтези', 'Антиутопия'],
	stats: {
		read: 47,
		favorites: 12,
		reviews: 8
	}
};

export const mockGenreOptions = [
	'Классика', 'Фантастика', 'Фэнтези', 'Детектив',
	'Антиутопия', 'Приключения', 'Исторический', 'Психологический',
	'Хоррор', 'Поэзия', 'Сатира', 'Магический реализм'
];

// ===== 10 оценок пользователя =====
// Привязаны к конкретным id из library.js. По ним же построен
// hardcoded-список «похожих» в recommendationsMocks.js.
// TODO: GET /api/profile/ratings
const RATED = [
	{ bookId: 1,  userRating: 5, ratedAt: '2026-05-18' }, // Гарри Поттер 1
	{ bookId: 12, userRating: 5, ratedAt: '2026-05-12' }, // Дюна
	{ bookId: 43, userRating: 5, ratedAt: '2026-05-02' }, // Мастер и Маргарита
	{ bookId: 64, userRating: 5, ratedAt: '2026-04-22' }, // 1984
	{ bookId: 35, userRating: 4, ratedAt: '2026-04-10' }, // Преступление и наказание
	{ bookId: 29, userRating: 4, ratedAt: '2026-03-28' }, // Этюд в багровых тонах
	{ bookId: 74, userRating: 5, ratedAt: '2026-03-12' }, // Имя ветра
	{ bookId: 59, userRating: 4, ratedAt: '2026-02-25' }, // Сияние
	{ bookId: 47, userRating: 5, ratedAt: '2026-02-08' }, // Пикник на обочине
	{ bookId: 82, userRating: 4, ratedAt: '2026-01-30' }  // Десять негритят
];

// Раскрываем оценки в полные карточки для вкладки «Мои оценки».
export const mockUserRatings = RATED.map((r) => {
	const b = findBook(r.bookId);
	return {
		bookId: r.bookId,
		title: b.title,
		author: b.author,
		cover: b.cover, // null → SVG
		userRating: r.userRating,
		ratedAt: r.ratedAt
	};
});

// ===== Избранное =====
// TODO: GET /api/favorites
const FAVORITE_IDS = [9, 12, 17, 64, 43, 22];
export const mockFavorites = FAVORITE_IDS.map((id) => {
	const b = findBook(id);
	return { id: b.id, title: b.title, author: b.author, rating: b.rating, cover: b.cover };
});

// ===== История просмотров =====
const HISTORY = [
	{ id: 1,  viewedAt: '2026-05-20T18:30:00' },
	{ id: 12, viewedAt: '2026-05-19T14:10:00' },
	{ id: 47, viewedAt: '2026-05-15T09:45:00' },
	{ id: 43, viewedAt: '2026-05-10T20:00:00' },
	{ id: 64, viewedAt: '2026-05-08T19:25:00' },
	{ id: 22, viewedAt: '2026-05-05T22:00:00' }
];
export const mockHistory = HISTORY.map(({ id, viewedAt }) => {
	const b = findBook(id);
	return {
		id: b.id, title: b.title, author: b.author,
		rating: b.rating, cover: b.cover, viewedAt
	};
});

// ===== Отзывы пользователя =====
export const mockUserReviews = [
	{
		bookId: 43, title: 'Мастер и Маргарита', rating: 5, reviewedAt: '2026-05-02',
		text: 'Потрясающая книга, читал за один вечер. Рекомендую абсолютно всем!'
	},
	{
		bookId: 64, title: '1984', rating: 5, reviewedAt: '2026-04-22',
		text: 'Очень актуально в наше время. Оруэлл был провидцем.'
	},
	{
		bookId: 1, title: 'Гарри Поттер и философский камень', rating: 5, reviewedAt: '2026-05-18',
		text: 'Та самая книга, с которой когда-то началась моя любовь к чтению.'
	}
];

// ===== Хронология добавлений в избранное =====
export const mockFavoriteAdditions = FAVORITE_IDS.map((id, idx) => {
	const b = findBook(id);
	return {
		bookId: id,
		title: b.title,
		author: b.author,
		// Раскладываем «добавления» по календарю с шагом ~неделя.
		addedAt: new Date(2026, 4, 18 - idx * 7).toISOString().slice(0, 10)
	};
});

// ===== Три карусели рекомендаций для вкладки «Рекомендации» =====
// Берём срезы из всей библиотеки — на бэке за это отвечают три эндпоинта.
const shuffleBySeed = (list, seed) => {
	const out = list.slice();
	let s = seed;
	for (let i = out.length - 1; i > 0; i--) {
		s = (s * 9301 + 49297) % 233280;
		const j = Math.floor((s / 233280) * (i + 1));
		[out[i], out[j]] = [out[j], out[i]];
	}
	return out;
};
export const mockRecommendationsForYou    = shuffleBySeed(BOOKS, 11).slice(0, 12);
export const mockRecommendationsByGenres  = shuffleBySeed(BOOKS, 22).slice(0, 12);
export const mockRecommendationsSimilar   = shuffleBySeed(BOOKS, 33).slice(0, 12);
