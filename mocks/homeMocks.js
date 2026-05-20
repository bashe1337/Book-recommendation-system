// === MOCK DATA — заменить на реальные эндпоинты когда бэкенд будет готов ===
//
// TODO: GET /api/recommendations/daily            -> heroBook
// TODO: GET /api/books/popular                    -> popularBooks
// TODO: GET /api/books/new                        -> newBooks
// TODO: GET /api/books/top-rated                  -> topRatedBooks
// TODO: GET /api/books/editors-choice             -> editorsChoice
// TODO: GET /api/recommendations/for-you          -> forYouBooks         (только авторизованные)
// TODO: GET /api/recommendations/by-genres        -> byGenresBooks       (только авторизованные)
// TODO: GET /api/recommendations/similar-viewed   -> similarViewedBooks  (только авторизованные)
//
// До подключения бэкенда все секции главной питаются из этих констант.

// Hero «Рекомендация дня».
export const heroBook = {
	id: 1,
	title: 'Мастер и Маргарита',
	author: 'Михаил Булгаков',
	cover: 'https://picsum.photos/seed/master/400/600',
	rating: 4.9,
	description:
		'Роман, в котором переплетаются реальность и фантастика, Москва 1930-х и древний Иерусалим. Дьявол приходит в советскую столицу, и ничто уже не будет прежним.'
};

// Базовый список книг для каруселей.
// Один и тот же список переиспользуется в разных секциях, чтобы
// имитировать ответы разных эндпоинтов без дублирования данных.
const mockBooks = [
	{ id: 1, title: 'Мастер и Маргарита', author: 'М. Булгаков', rating: 4.9, cover: 'https://picsum.photos/seed/book1/200/300' },
	{ id: 2, title: 'Преступление и наказание', author: 'Ф. Достоевский', rating: 4.8, cover: 'https://picsum.photos/seed/book2/200/300' },
	{ id: 3, title: '1984', author: 'Дж. Оруэлл', rating: 4.7, cover: 'https://picsum.photos/seed/book3/200/300' },
	{ id: 4, title: 'Война и мир', author: 'Л. Толстой', rating: 4.6, cover: 'https://picsum.photos/seed/book4/200/300' },
	{ id: 5, title: 'Идиот', author: 'Ф. Достоевский', rating: 4.5, cover: 'https://picsum.photos/seed/book5/200/300' },
	{ id: 6, title: 'Анна Каренина', author: 'Л. Толстой', rating: 4.4, cover: 'https://picsum.photos/seed/book6/200/300' },
	{ id: 7, title: 'Братья Карамазовы', author: 'Ф. Достоевский', rating: 4.3, cover: 'https://picsum.photos/seed/book7/200/300' },
	{ id: 8, title: 'Мёртвые души', author: 'Н. Гоголь', rating: 4.2, cover: 'https://picsum.photos/seed/book8/200/300' }
];

// Утилита: возвращает копию списка, перемешанную детерминированно по seed.
// Нужна, чтобы каждая карусель не выглядела абсолютно одинаково —
// порядок книг чуть отличается, как если бы данные пришли от разных API.
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

// Срезы для каруселей. По требованию ТЗ — на всех должен работать один
// mockBooks. Меняем только порядок, чтобы UI выглядел живо.
export const popularBooks      = shuffleBySeed(mockBooks, 1);
export const newBooks          = shuffleBySeed(mockBooks, 2);
export const topRatedBooks     = [...mockBooks].sort((a, b) => b.rating - a.rating);
export const editorsChoice     = shuffleBySeed(mockBooks, 4);

// Авторизованные секции.
export const forYouBooks        = shuffleBySeed(mockBooks, 5);
export const byGenresBooks      = shuffleBySeed(mockBooks, 6);
export const similarViewedBooks = shuffleBySeed(mockBooks, 7);
