// === MOCK DATA — персональные рекомендации ===
//
// На презентации рекомендации показаны как «похожие на оценённые
// пользователем книги» (см. mockUserRatings в profileMocks.js).
// Это статический хардкод — настоящий рекомендательный движок появится
// позже отдельным модулем.
//
// TODO: GET /api/recommendations/for-you?type=...&genres=...&minRating=...&excludeRead=...
//   Бэкенд будет формировать список через:
//     - content-based (TF-IDF по жанрам и тегам)
//     - item-based collaborative filtering
//     - popularity-based для холодного старта.

import { findBook } from './library.js';

// Каждая запись — пара (id рекомендованной книги; id оценённой книги,
// по которой и появилась эта рекомендация; тип/подпись бейджа).
//
// Типы и цвета:
//   series        → primary   «Из серии»
//   author        → secondary «Тот же автор»
//   similar       → success   «Похожее»
//   genre         → warning   «Ваш жанр»
const SEEDS = [
	// === Похожие на Гарри Поттер 1 (книга #1) ===
	{ id: 2, fromId: 1, type: 'series', label: 'Из серии' },
	{ id: 3, fromId: 1, type: 'series', label: 'Из серии' },
	{ id: 4, fromId: 1, type: 'series', label: 'Из серии' },
	{ id: 5, fromId: 1, type: 'series', label: 'Из серии' },
	{ id: 6, fromId: 1, type: 'series', label: 'Из серии' },
	{ id: 7, fromId: 1, type: 'series', label: 'Из серии' },
	{ id: 8, fromId: 1, type: 'similar', label: 'Похожее' },

	// === Похожие на Дюну (#12) ===
	{ id: 13, fromId: 12, type: 'series', label: 'Из серии' },
	{ id: 14, fromId: 12, type: 'series', label: 'Из серии' },
	{ id: 15, fromId: 12, type: 'series', label: 'Из серии' },
	{ id: 54, fromId: 12, type: 'similar', label: 'Похожее' },
	{ id: 51, fromId: 12, type: 'genre', label: 'Ваш жанр' },

	// === Похожие на Мастера и Маргариту (#43) ===
	{ id: 44, fromId: 43, type: 'author',  label: 'Тот же автор' },
	{ id: 45, fromId: 43, type: 'author',  label: 'Тот же автор' },
	{ id: 46, fromId: 43, type: 'author',  label: 'Тот же автор' },

	// === Похожие на 1984 (#64) — антиутопии ===
	{ id: 65, fromId: 64, type: 'author',  label: 'Тот же автор' },
	{ id: 66, fromId: 64, type: 'similar', label: 'Похожее' },
	{ id: 67, fromId: 64, type: 'similar', label: 'Похожее' },
	{ id: 68, fromId: 64, type: 'similar', label: 'Похожее' },

	// === Похожие на Преступление и наказание (#35) ===
	{ id: 36, fromId: 35, type: 'author', label: 'Тот же автор' },
	{ id: 37, fromId: 35, type: 'author', label: 'Тот же автор' },
	{ id: 38, fromId: 35, type: 'author', label: 'Тот же автор' },

	// === Похожие на Этюд в багровых тонах (#29) — остальной Шерлок ===
	{ id: 30, fromId: 29, type: 'series', label: 'Из серии' },
	{ id: 31, fromId: 29, type: 'series', label: 'Из серии' },
	{ id: 32, fromId: 29, type: 'series', label: 'Из серии' },
	{ id: 33, fromId: 29, type: 'series', label: 'Из серии' },

	// === Похожие на Имя ветра (#74) ===
	{ id: 75, fromId: 74, type: 'series',  label: 'Из серии' },
	{ id: 71, fromId: 74, type: 'similar', label: 'Похожее' },

	// === Похожие на Сияние (#59) — остальной Кинг ===
	{ id: 60, fromId: 59, type: 'author', label: 'Тот же автор' },
	{ id: 61, fromId: 59, type: 'author', label: 'Тот же автор' },
	{ id: 62, fromId: 59, type: 'author', label: 'Тот же автор' },
	{ id: 63, fromId: 59, type: 'author', label: 'Тот же автор' },

	// === Похожие на Пикник на обочине (#47) — остальные Стругацкие ===
	{ id: 48, fromId: 47, type: 'author', label: 'Тот же автор' },
	{ id: 49, fromId: 47, type: 'author', label: 'Тот же автор' },
	{ id: 50, fromId: 47, type: 'author', label: 'Тот же автор' },

	// === Похожие на Десять негритят (#82) — остальная Кристи ===
	{ id: 83, fromId: 82, type: 'series', label: 'Из серии' },
	{ id: 84, fromId: 82, type: 'series', label: 'Из серии' },
	{ id: 85, fromId: 82, type: 'series', label: 'Из серии' }
];

// Разворачиваем seeds в полные карточки + информацию о причине рекомендации.
// Если по какому-то id в библиотеке книги нет — просто молча пропускаем,
// чтобы движок не падал при правке library.js.
export const mockRecommendations = SEEDS
	.map((seed) => {
		const book = findBook(seed.id);
		const from = findBook(seed.fromId);
		if (!book || !from) return null;
		return {
			id: book.id,
			title: book.title,
			author: book.author,
			cover: book.cover,
			rating: book.rating,
			genre: book.genres[0],
			isRead: false,
			recommendationType: seed.type,
			recommendationLabel: seed.label,
			// На какой именно оценке держится рекомендация — показываем
			// мелким курсивом под бейджем, для презентации убедительно.
			recommendationFrom: from.title
		};
	})
	.filter(Boolean);

// Цвета бейджей причины рекомендации.
export const RECOMMENDATION_BADGE_COLORS = {
	series:        'primary',
	author:        'secondary',
	similar:       'success',
	genre:         'warning',
	popular:       'info',
	collaborative: 'success'
};

// Сводка для счётчиков-чипов в шапке (из mockUserRatings в профиле).
// TODO: GET /api/profile/me → возвращает эти три цифры.
export const mockUserSummary = {
	ratingsCount: 10,
	favoriteGenresCount: 4,
	viewedBooksCount: 47
};
