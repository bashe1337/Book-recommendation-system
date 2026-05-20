// === MOCK DATA — убрать, когда бэкенд будет готов ===
//
// TODO: GET /api/books/search?q=...&sort=relevance|rating|year — основной поиск
// TODO: GET /api/books/suggest?q=...                            — подсказки (pg_trgm)
// TODO: GET /api/books/expand?q=...                             — query expansion (синонимы)
//
// Бэкенд использует PostgreSQL Full Text Search + pg_trgm.
// Здесь имитируем триграммное сравнение через substring + fuzzy на 1–2 опечатки.

export const mockBooks = [
	{ id: 1,  title: 'Мастер и Маргарита',                 author: 'М. Булгаков',     genre: 'Классика',   year: 1967, lang: 'Русский',    rating: 4.9, cover: 'https://picsum.photos/seed/s1/80/120',  description: 'Роман о визите Воланда в советскую Москву и истории Мастера и его возлюбленной Маргариты.' },
	{ id: 2,  title: 'Преступление и наказание',           author: 'Ф. Достоевский',  genre: 'Классика',   year: 1866, lang: 'Русский',    rating: 4.8, cover: 'https://picsum.photos/seed/s2/80/120',  description: 'История студента Раскольникова, совершившего убийство и его нравственные терзания.' },
	{ id: 3,  title: '1984',                                author: 'Дж. Оруэлл',      genre: 'Антиутопия', year: 1949, lang: 'Английский', rating: 4.7, cover: 'https://picsum.photos/seed/s3/80/120',  description: 'Роман-антиутопия о тоталитарном обществе под надзором Большого Брата.' },
	{ id: 4,  title: 'Дюна',                                author: 'Ф. Херберт',      genre: 'Фантастика', year: 1965, lang: 'Английский', rating: 4.7, cover: 'https://picsum.photos/seed/s4/80/120',  description: 'Эпическая сага о пустынной планете Арракис и судьбе Пола Атрейдеса.' },
	{ id: 5,  title: 'Солярис',                             author: 'С. Лем',           genre: 'Фантастика', year: 1961, lang: 'Русский',    rating: 4.6, cover: 'https://picsum.photos/seed/s5/80/120',  description: 'Философский роман о контакте человечества с непостижимым инопланетным разумом.' },
	{ id: 6,  title: 'Война и мир',                         author: 'Л. Толстой',      genre: 'Классика',   year: 1869, lang: 'Русский',    rating: 4.6, cover: 'https://picsum.photos/seed/s6/80/120',  description: 'Монументальный роман об эпохе наполеоновских войн и судьбах русских семей.' },
	{ id: 7,  title: 'Властелин колец',                     author: 'Дж. Толкин',      genre: 'Фэнтези',    year: 1954, lang: 'Английский', rating: 4.9, cover: 'https://picsum.photos/seed/s7/80/120',  description: 'Легендарная эпопея о борьбе за Кольцо Всевластия в Средиземье.' },
	{ id: 8,  title: 'Десять негритят',                     author: 'А. Кристи',       genre: 'Детектив',   year: 1939, lang: 'Русский',    rating: 4.6, cover: 'https://picsum.photos/seed/s8/80/120',  description: 'Десять незнакомцев приглашены на остров, откуда нет спасения.' },
	{ id: 9,  title: 'Собачье сердце',                      author: 'М. Булгаков',     genre: 'Классика',   year: 1925, lang: 'Русский',    rating: 4.7, cover: 'https://picsum.photos/seed/s9/80/120',  description: 'Сатирическая повесть о профессоре Преображенском и его необычном эксперименте.' },
	{ id: 10, title: 'Гиперион',                            author: 'Д. Симмонс',      genre: 'Фантастика', year: 1989, lang: 'Русский',    rating: 4.8, cover: 'https://picsum.photos/seed/s10/80/120', description: 'Семеро паломников держат путь к таинственным Гробницам Времени на планете Гиперион.' },
	{ id: 11, title: 'Идиот',                               author: 'Ф. Достоевский',  genre: 'Классика',   year: 1869, lang: 'Русский',    rating: 4.5, cover: 'https://picsum.photos/seed/s11/80/120', description: 'История князя Мышкина — человека редкой душевной чистоты в мире лжи и корысти.' },
	{ id: 12, title: 'Гарри Поттер и философский камень',   author: 'Дж. Роулинг',     genre: 'Фэнтези',    year: 1997, lang: 'Английский', rating: 4.8, cover: 'https://picsum.photos/seed/s12/80/120', description: 'Мальчик-волшебник поступает в школу магии Хогвартс и узнаёт о своём предназначении.' }
];

// Популярные запросы для начального экрана (когда нет ?q=...).
export const mockPopularQueries = [
	'Булгаков', 'Достоевский', 'Фантастика', 'Детектив',
	'Классика', 'Толстой', 'Оруэлл', 'Фэнтези'
];

// Словарь синонимов/расширений запросов.
// На бэке это построено по словарю синонимов и word2vec/fasttext.
export const mockQueryExpansions = {
	'булгаков':   ['Мастер и Маргарита', 'Собачье сердце', 'Белая гвардия', 'мистика'],
	'фантастика': ['Дюна', 'Солярис', 'Гиперион', '1984', 'антиутопия'],
	'детектив':   ['Шерлок Холмс', 'Агата Кристи', 'Десять негритят', 'криминал'],
	'толстой':    ['Война и мир', 'Анна Каренина', 'классика', 'русская литература']
};

// ---- Утилиты ----

// Имитация fuzzy-сравнения «расстояние ≤2 опечатки» по позиционно.
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

// Подсказки для автокомплита: до 8 элементов, метим тип совпадения
// (title / author / genre) — это используется при отрисовке.
// TODO: GET /api/books/suggest?q=...
export const getSuggestions = (query) => {
	if (!query || query.length < 2) return [];
	const q = query.toLowerCase().trim();

	return mockBooks
		.filter((b) =>
			b.title.toLowerCase().includes(q) ||
			b.author.toLowerCase().includes(q) ||
			b.genre.toLowerCase().includes(q) ||
			fuzzyMatch(b.title.toLowerCase(), q) ||
			fuzzyMatch(b.author.toLowerCase(), q)
		)
		.slice(0, 8)
		.map((b) => {
			let type = 'title';
			if (!b.title.toLowerCase().includes(q)) {
				if (b.author.toLowerCase().includes(q))      type = 'author';
				else if (b.genre.toLowerCase().includes(q))  type = 'genre';
			}
			return {
				id: b.id,
				label: b.title,
				sublabel: b.author,
				type
			};
		});
};

// Основной поиск: фильтр + сортировка по релевантности с тай-брейкером по рейтингу.
// Точное вхождение в title всегда поднимает книгу выше.
// TODO: GET /api/books/search?q=...&sort=relevance|rating|year
export const performSearch = (query) => {
	if (!query || query.length < 2) return [];
	const q = query.toLowerCase().trim();

	return mockBooks
		.filter((b) =>
			b.title.toLowerCase().includes(q) ||
			b.author.toLowerCase().includes(q) ||
			b.genre.toLowerCase().includes(q) ||
			b.description.toLowerCase().includes(q) ||
			fuzzyMatch(b.title.toLowerCase(), q) ||
			fuzzyMatch(b.author.toLowerCase(), q)
		)
		.sort((a, b) => {
			const aExact = a.title.toLowerCase().includes(q) ? 1 : 0;
			const bExact = b.title.toLowerCase().includes(q) ? 1 : 0;
			return bExact - aExact || b.rating - a.rating;
		});
};

// Query expansion: словарные синонимы, если запрос есть в словаре,
// иначе — fallback на жанры из найденных результатов.
// TODO: GET /api/books/expand?q=...
export const getExpansions = (query, results) => {
	if (!query) return [];
	const q = query.toLowerCase().trim();
	if (mockQueryExpansions[q]) return mockQueryExpansions[q];
	return [...new Set((results || []).map((b) => b.genre))].slice(0, 4);
};

// Подсветка совпадения. Возвращает HTML — рендерить через v-html.
// Экранируем спецсимволы regex, чтобы запрос «c++» не падал.
// Также экранируем & < > в исходном тексте, чтобы избежать XSS в v-html.
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
