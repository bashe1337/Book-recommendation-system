// === MOCK DATA — убрать, когда бэкенд будет готов ===
//
// TODO: GET /api/profile/me                   — данные текущего пользователя и метрики
// TODO: PUT /api/profile/me                   — обновить профиль
// TODO: POST /api/auth/logout                 — выход из аккаунта
// TODO: GET /api/favorites                    — избранное
// TODO: DELETE /api/favorites/:bookId         — убрать из избранного
// TODO: GET /api/reading-history              — история просмотров
// TODO: DELETE /api/reading-history           — очистить историю
// TODO: GET /api/profile/ratings              — оценки пользователя
// TODO: GET /api/profile/reviews              — отзывы пользователя
// TODO: PUT /api/ratings/:bookId              — изменить оценку
// TODO: GET /api/recommendations/for-you
// TODO: GET /api/recommendations/by-genres
// TODO: GET /api/recommendations/similar-viewed

export const mockUser = {
	id: 42,
	name: 'Александр Петров',
	email: 'alex.petrov@example.com',
	avatar: null,
	registeredAt: '2024-03-10',
	// дефолтный выбор предпочтений; см. mockGenreOptions ниже
	preferredGenres: ['Классика', 'Фантастика', 'Антиутопия'],
	stats: {
		read: 47,
		favorites: 12,
		reviews: 8
	}
};

export const mockGenreOptions = [
	'Классика', 'Фантастика', 'Фэнтези', 'Детектив', 'Антиутопия',
	'Приключения', 'Исторический роман', 'Психология', 'Биография', 'Поэзия'
];

export const mockFavorites = [
	{ id: 3, title: '1984',             author: 'Дж. Оруэлл', rating: 4.7, cover: 'https://picsum.photos/seed/fav1/200/300' },
	{ id: 4, title: 'Дюна',             author: 'Ф. Херберт', rating: 4.7, cover: 'https://picsum.photos/seed/fav2/200/300' },
	{ id: 8, title: 'Властелин колец',  author: 'Дж. Толкин', rating: 4.9, cover: 'https://picsum.photos/seed/fav3/200/300' }
];

export const mockHistory = [
	{ id: 1,  title: 'Мастер и Маргарита',        author: 'М. Булгаков',    rating: 4.9, cover: 'https://picsum.photos/seed/hist1/50/75', viewedAt: '2026-05-20T18:30:00' },
	{ id: 2,  title: 'Преступление и наказание',  author: 'Ф. Достоевский', rating: 4.8, cover: 'https://picsum.photos/seed/hist2/50/75', viewedAt: '2026-05-19T14:10:00' },
	{ id: 5,  title: 'Солярис',                    author: 'С. Лем',         rating: 4.6, cover: 'https://picsum.photos/seed/hist3/50/75', viewedAt: '2026-05-15T09:45:00' },
	{ id: 12, title: 'Гиперион',                   author: 'Д. Симмонс',     rating: 4.8, cover: 'https://picsum.photos/seed/hist4/50/75', viewedAt: '2026-05-10T20:00:00' }
];

export const mockUserRatings = [
	{ bookId: 1, title: 'Мастер и Маргарита', author: 'М. Булгаков',  cover: 'https://picsum.photos/seed/rat1/50/75', userRating: 5, ratedAt: '2026-05-18' },
	{ bookId: 3, title: '1984',                author: 'Дж. Оруэлл',  cover: 'https://picsum.photos/seed/rat2/50/75', userRating: 4, ratedAt: '2026-04-30' }
];

export const mockUserReviews = [
	{
		bookId: 1, title: 'Мастер и Маргарита', rating: 5, reviewedAt: '2026-05-18',
		text: 'Потрясающая книга, читал за один вечер. Рекомендую абсолютно всем!'
	},
	{
		bookId: 3, title: '1984', rating: 4, reviewedAt: '2026-04-30',
		text: 'Очень актуально в наше время. Оруэлл был провидцем.'
	}
];

// Добавления в избранное (хронология).
// На реальном бэке это часть профиля — здесь имитируем датами добавления.
export const mockFavoriteAdditions = [
	{ bookId: 3, title: '1984',            author: 'Дж. Оруэлл', addedAt: '2026-05-12' },
	{ bookId: 4, title: 'Дюна',            author: 'Ф. Херберт', addedAt: '2026-04-28' },
	{ bookId: 8, title: 'Властелин колец', author: 'Дж. Толкин', addedAt: '2026-03-15' }
];

// Для рекомендаций — отдельные срезы.
// В реальности придут три разных ответа от трёх эндпоинтов.
export const mockRecommendationsForYou = [
	{ id: 1, title: 'Мастер и Маргарита',         author: 'М. Булгаков',    rating: 4.9, cover: 'https://picsum.photos/seed/rec-fy1/200/300' },
	{ id: 5, title: 'Солярис',                     author: 'С. Лем',         rating: 4.6, cover: 'https://picsum.photos/seed/rec-fy2/200/300' },
	{ id: 7, title: 'Гарри Поттер',                author: 'Дж. Роулинг',    rating: 4.8, cover: 'https://picsum.photos/seed/rec-fy3/200/300' },
	{ id: 9, title: 'Граф Монте-Кристо',           author: 'А. Дюма',         rating: 4.7, cover: 'https://picsum.photos/seed/rec-fy4/200/300' },
	{ id: 11, title: 'Десять негритят',            author: 'А. Кристи',       rating: 4.6, cover: 'https://picsum.photos/seed/rec-fy5/200/300' },
	{ id: 12, title: 'Гиперион',                   author: 'Д. Симмонс',     rating: 4.8, cover: 'https://picsum.photos/seed/rec-fy6/200/300' }
];

export const mockRecommendationsByGenres = [
	{ id: 6,  title: 'Война и мир',           author: 'Л. Толстой',      rating: 4.6, cover: 'https://picsum.photos/seed/rec-bg1/200/300' },
	{ id: 2,  title: 'Преступление и наказание', author: 'Ф. Достоевский', rating: 4.8, cover: 'https://picsum.photos/seed/rec-bg2/200/300' },
	{ id: 3,  title: '1984',                   author: 'Дж. Оруэлл',     rating: 4.7, cover: 'https://picsum.photos/seed/rec-bg3/200/300' },
	{ id: 4,  title: 'Дюна',                   author: 'Ф. Херберт',     rating: 4.7, cover: 'https://picsum.photos/seed/rec-bg4/200/300' },
	{ id: 8,  title: 'Властелин колец',        author: 'Дж. Толкин',     rating: 4.9, cover: 'https://picsum.photos/seed/rec-bg5/200/300' },
	{ id: 13, title: 'Туманность Андромеды',  author: 'И. Ефремов',     rating: 4.4, cover: 'https://picsum.photos/seed/rec-bg6/200/300' }
];

export const mockRecommendationsSimilar = [
	{ id: 14, title: 'Собачье сердце',          author: 'М. Булгаков',  rating: 4.7, cover: 'https://picsum.photos/seed/rec-sv1/200/300' },
	{ id: 15, title: 'Белая гвардия',           author: 'М. Булгаков',  rating: 4.5, cover: 'https://picsum.photos/seed/rec-sv2/200/300' },
	{ id: 16, title: 'Роковые яйца',            author: 'М. Булгаков',  rating: 4.3, cover: 'https://picsum.photos/seed/rec-sv3/200/300' },
	{ id: 17, title: 'Двенадцать стульев',      author: 'И. Ильф',      rating: 4.6, cover: 'https://picsum.photos/seed/rec-sv4/200/300' },
	{ id: 18, title: 'Золотой телёнок',         author: 'И. Ильф',      rating: 4.5, cover: 'https://picsum.photos/seed/rec-sv5/200/300' },
	{ id: 19, title: 'Театральный роман',       author: 'М. Булгаков',  rating: 4.4, cover: 'https://picsum.photos/seed/rec-sv6/200/300' }
];
