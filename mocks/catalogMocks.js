// === MOCK DATA — убрать, когда бэкенд будет готов ===
// TODO: GET /api/books            — список всех книг с фильтрами и сортировкой
// TODO: GET /api/books/search     — поиск с подсказками (pg_trgm на бэке)
// TODO: GET /api/books/filters    — справочники жанров/авторов/языков

export const mockBooks = [
	{ id: 1,  title: 'Мастер и Маргарита',                  author: 'М. Булгаков',    genre: 'Классика',    year: 1967, lang: 'Русский',    rating: 4.9, cover: 'https://picsum.photos/seed/book1/200/300' },
	{ id: 2,  title: 'Преступление и наказание',            author: 'Ф. Достоевский', genre: 'Классика',    year: 1866, lang: 'Русский',    rating: 4.8, cover: 'https://picsum.photos/seed/book2/200/300' },
	{ id: 3,  title: '1984',                                 author: 'Дж. Оруэлл',     genre: 'Антиутопия',  year: 1949, lang: 'Английский', rating: 4.7, cover: 'https://picsum.photos/seed/book3/200/300' },
	{ id: 4,  title: 'Дюна',                                 author: 'Ф. Херберт',     genre: 'Фантастика',  year: 1965, lang: 'Английский', rating: 4.7, cover: 'https://picsum.photos/seed/book4/200/300' },
	{ id: 5,  title: 'Солярис',                              author: 'С. Лем',          genre: 'Фантастика',  year: 1961, lang: 'Русский',    rating: 4.6, cover: 'https://picsum.photos/seed/book5/200/300' },
	{ id: 6,  title: 'Война и мир',                          author: 'Л. Толстой',     genre: 'Классика',    year: 1869, lang: 'Русский',    rating: 4.6, cover: 'https://picsum.photos/seed/book6/200/300' },
	{ id: 7,  title: 'Гарри Поттер и философский камень',    author: 'Дж. Роулинг',    genre: 'Фэнтези',     year: 1997, lang: 'Английский', rating: 4.8, cover: 'https://picsum.photos/seed/book7/200/300' },
	{ id: 8,  title: 'Властелин колец',                      author: 'Дж. Толкин',     genre: 'Фэнтези',     year: 1954, lang: 'Английский', rating: 4.9, cover: 'https://picsum.photos/seed/book8/200/300' },
	{ id: 9,  title: 'Граф Монте-Кристо',                    author: 'А. Дюма',         genre: 'Приключения', year: 1844, lang: 'Русский',    rating: 4.7, cover: 'https://picsum.photos/seed/book9/200/300' },
	{ id: 10, title: 'Шерлок Холмс',                         author: 'А. Конан Дойл',  genre: 'Детектив',    year: 1887, lang: 'Русский',    rating: 4.5, cover: 'https://picsum.photos/seed/book10/200/300' },
	{ id: 11, title: 'Десять негритят',                      author: 'А. Кристи',      genre: 'Детектив',    year: 1939, lang: 'Русский',    rating: 4.6, cover: 'https://picsum.photos/seed/book11/200/300' },
	{ id: 12, title: 'Гиперион',                             author: 'Д. Симмонс',     genre: 'Фантастика',  year: 1989, lang: 'Русский',    rating: 4.8, cover: 'https://picsum.photos/seed/book12/200/300' }
];

export const mockGenres    = ['Классика', 'Фантастика', 'Антиутопия', 'Фэнтези', 'Детектив', 'Приключения', 'Романтика', 'Ужасы', 'Биография'];
export const mockAuthors   = [...new Set(mockBooks.map((b) => b.author))];
export const mockLanguages = ['Русский', 'Английский', 'Немецкий', 'Французский', 'Испанский'];

// Границы фильтра по году издания.
export const YEAR_MIN = 1900;
export const YEAR_MAX = 2025;

// Имитация триграммного поиска: substring match по title и author.
// TODO: заменить на GET /api/books/search?q=...&suggest=true
export const getSuggestions = (query) => {
	if (!query || query.length < 2) return [];
	const q = query.toLowerCase();
	return mockBooks
		.filter(
			(b) =>
				b.title.toLowerCase().includes(q) ||
				b.author.toLowerCase().includes(q)
		)
		.slice(0, 6)
		.map((b) => ({ label: `${b.title} — ${b.author}`, id: b.id, title: b.title, author: b.author }));
};
