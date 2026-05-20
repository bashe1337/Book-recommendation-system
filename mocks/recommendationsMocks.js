// === MOCK DATA — персональные рекомендации ===
// TODO: GET /api/recommendations/for-you?type=all&genres=...&minRating=...&excludeRead=true
// Бэкенд формирует рекомендации через гибридный модуль:
//   content-based (TF-IDF по жанрам и описаниям) +
//   item-based collaborative filtering +
//   popularity-based для новых пользователей.

export const mockRecommendations = [
	{ id: 4,  title: 'Дюна',                                  author: 'Ф. Херберт',         genre: 'Фантастика',  rating: 4.7, isRead: false, cover: 'https://picsum.photos/seed/rec1/200/300',  recommendationType: 'genre',         recommendationLabel: 'Ваш жанр' },
	{ id: 7,  title: 'Властелин колец',                       author: 'Дж. Толкин',         genre: 'Фэнтези',     rating: 4.9, isRead: true,  cover: 'https://picsum.photos/seed/rec2/200/300',  recommendationType: 'collaborative', recommendationLabel: 'Читают как вы' },
	{ id: 9,  title: 'Собачье сердце',                        author: 'М. Булгаков',        genre: 'Классика',    rating: 4.7, isRead: false, cover: 'https://picsum.photos/seed/rec3/200/300',  recommendationType: 'similar',       recommendationLabel: 'Похожая' },
	{ id: 10, title: 'Гиперион',                               author: 'Д. Симмонс',         genre: 'Фантастика',  rating: 4.8, isRead: false, cover: 'https://picsum.photos/seed/rec4/200/300',  recommendationType: 'genre',         recommendationLabel: 'Ваш жанр' },
	{ id: 11, title: 'Двенадцать стульев',                    author: 'И. Ильф, Е. Петров', genre: 'Классика',    rating: 4.6, isRead: false, cover: 'https://picsum.photos/seed/rec5/200/300',  recommendationType: 'popular',       recommendationLabel: 'Популярное' },
	{ id: 12, title: 'Граф Монте-Кристо',                     author: 'А. Дюма',            genre: 'Приключения', rating: 4.7, isRead: true,  cover: 'https://picsum.photos/seed/rec6/200/300',  recommendationType: 'collaborative', recommendationLabel: 'Читают как вы' },
	{ id: 13, title: 'Шерлок Холмс',                          author: 'А. Конан Дойл',      genre: 'Детектив',    rating: 4.5, isRead: false, cover: null,                                       recommendationType: 'genre',         recommendationLabel: 'Ваш жанр' },
	{ id: 14, title: 'Убийство в Восточном экспрессе',        author: 'А. Кристи',          genre: 'Детектив',    rating: 4.6, isRead: false, cover: null,                                       recommendationType: 'similar',       recommendationLabel: 'Похожая' },
	{ id: 15, title: 'О дивный новый мир',                    author: 'О. Хаксли',          genre: 'Антиутопия',  rating: 4.5, isRead: false, cover: 'https://picsum.photos/seed/rec9/200/300',  recommendationType: 'similar',       recommendationLabel: 'Похожая' },
	{ id: 16, title: 'Fahrenheit 451',                         author: 'Р. Брэдбери',        genre: 'Антиутопия',  rating: 4.6, isRead: false, cover: 'https://picsum.photos/seed/rec10/200/300', recommendationType: 'collaborative', recommendationLabel: 'Читают как вы' },
	{ id: 17, title: 'Белая гвардия',                          author: 'М. Булгаков',        genre: 'Классика',    rating: 4.5, isRead: false, cover: 'https://picsum.photos/seed/rec11/200/300', recommendationType: 'genre',         recommendationLabel: 'Ваш жанр' },
	{ id: 18, title: 'Пикник на обочине',                      author: 'А. и Б. Стругацкие', genre: 'Фантастика',  rating: 4.7, isRead: false, cover: null,                                       recommendationType: 'popular',       recommendationLabel: 'Популярное' }
];

// Цвет бейджа в зависимости от типа рекомендации (для v-chip color).
export const RECOMMENDATION_BADGE_COLORS = {
	genre:         'primary',
	similar:       'secondary',
	collaborative: 'success',
	popular:       'warning'
};

// Сводка для счётчиков-чипов в шапке.
// TODO: GET /api/profile/me → возвращает эти три цифры.
export const mockUserSummary = {
	ratingsCount: 23,
	favoriteGenresCount: 4,
	viewedBooksCount: 47
};
