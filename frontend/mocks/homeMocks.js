// === MOCK DATA — заменить на реальные эндпоинты когда бэкенд будет готов ===
// Все секции главной — это срезы из единой библиотеки (mocks/library.js).
// Так на главной, в каталоге, в поиске и на странице книги переиспользуются
// одни и те же данные, и переход «карточка → /book/:id» всегда работает.
//
// TODO: GET /api/recommendations/daily            -> heroBook
// TODO: GET /api/books/popular                    -> popularBooks
// TODO: GET /api/books/new                        -> newBooks
// TODO: GET /api/books/top-rated                  -> topRatedBooks
// TODO: GET /api/books/editors-choice             -> editorsChoice
// TODO: GET /api/recommendations/for-you          -> forYouBooks         (только авторизованные)
// TODO: GET /api/recommendations/by-genres        -> byGenresBooks       (только авторизованные)
// TODO: GET /api/recommendations/similar-viewed   -> similarViewedBooks  (только авторизованные)

import { BOOKS, findBook } from './library.js';

// Hero «Рекомендация дня». Берём конкретную книгу из библиотеки —
// чтобы клик «Подробнее» вёл на существующую /book/:id.
export const heroBook = (() => {
	const b = findBook(43); // «Мастер и Маргарита»
	return {
		id: b.id,
		title: b.title,
		author: b.author,
		cover: b.cover, // null → SVG-заглушка
		rating: b.rating,
		description: b.description
	};
})();

// Утилита: детерминированно перетасовываем массив по seed.
// Нужно, чтобы карусели на главной выглядели разнообразно,
// будто пришли из разных эндпоинтов.
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

// «Популярное» — топ по рейтингу из библиотеки.
export const popularBooks = [...BOOKS].sort((a, b) => b.rating - a.rating).slice(0, 16);

// «Новинки» — последние по году.
export const newBooks = [...BOOKS].sort((a, b) => b.year - a.year).slice(0, 16);

// «Рейтинг» — выводим строго по рейтингу.
export const topRatedBooks = [...BOOKS].sort((a, b) => b.rating - a.rating).slice(0, 12);

// «Выбор редакции» — перетасовка для разнообразия.
export const editorsChoice = shuffleBySeed(BOOKS, 4).slice(0, 12);

// Для авторизованного пользователя — три персональных карусели.
// На реальном бэке их формирует рекомендательный модуль.
export const forYouBooks        = shuffleBySeed(BOOKS, 5).slice(0, 12);
export const byGenresBooks      = shuffleBySeed(BOOKS, 6).slice(0, 12);
export const similarViewedBooks = shuffleBySeed(BOOKS, 7).slice(0, 12);
