<template>
	<div class="search-page" :class="{ 'search-page--initial': !hasQuery }">
		<v-container class="search-page__container">
			<h1 class="search-page__heading">Поиск</h1>
			<p v-if="!hasQuery" class="search-page__lead">
				Введите название книги, имя автора или жанр — мы предложим
				подсказки по&nbsp;мере ввода
			</p>

			<!--
				v-autocomplete с подсказками по GET /api/search/autocomplete
				и дебаунсом 300мс. При выборе подсказки запускаем поиск
				и запоминаем книгу для блока «Рекомендуем похожее».
			-->
			<div class="search-page__search-row">
				<v-autocomplete
					v-model="selectedSuggestion"
					v-model:search="searchInput"
					:items="suggestions"
					item-title="text"
					item-value="id"
					placeholder="Поиск книги или автора…"
					prepend-inner-icon="mdi-magnify"
					variant="outlined"
					density="comfortable"
					hide-details
					hide-no-data
					clearable
					no-filter
					return-object
					rounded="pill"
					class="search-page__input"
					@keyup.enter="onEnter"
					@update:model-value="onSelectSuggestion"
					@click:clear="resetSearch"
				>
					<template #item="{ item, props }">
						<v-list-item v-bind="props" :title="undefined">
							<template #prepend>
								<v-icon size="18" color="grey">
									{{ suggestionIcon(item.raw.type) }}
								</v-icon>
							</template>
							<div class="suggestion-row">
								<div
									class="suggestion-row__label"
									v-html="highlight(item.raw.text)"
								/>
							</div>
							<template #append>
								<span class="suggestion-row__type">
									{{ suggestionTypeLabel(item.raw.type) }}
								</span>
							</template>
						</v-list-item>
					</template>
				</v-autocomplete>
			</div>

			<!-- Начальный экран — без запроса. -->
			<section v-if="!hasQuery" class="popular">
				<div class="popular__title">Начните с популярного</div>
				<div class="popular__chips">
					<v-chip
						v-for="q in popularQueries"
						:key="q"
						variant="outlined"
						class="popular__chip"
						@click="runSearch(q)"
					>
						{{ q }}
					</v-chip>
				</div>
			</section>

			<!-- Skeleton пока работает дебаунс или идёт запрос. -->
			<section v-else-if="isSearching" class="results">
				<v-skeleton-loader
					v-for="i in 8"
					:key="i"
					type="image"
					class="results__skeleton"
				/>
			</section>

			<section v-else-if="results.length" class="results">
				<div class="results__header">
					<div class="results__summary">
						Найдено результатов:
						<b>{{ totalResults }}</b>
						по запросу «{{ debouncedQuery }}»
					</div>
				</div>

				<v-row dense>
					<v-col
						v-for="book in results"
						:key="book.id"
						cols="6"
						sm="4"
						md="3"
						lg="2"
					>
						<BookCard :book="book" />
					</v-col>
				</v-row>

				<!--
					«Рекомендуем похожее» — GET /api/books/{id}/recommendations.
					Появляется, когда пользователь выбрал конкретную книгу из подсказок,
					либо когда результат единственный.
				-->
				<section v-if="similarToSelected.length" class="similar-block">
					<div class="similar-block__heading">
						<h2 class="similar-block__title">Рекомендуем похожее</h2>
						<div class="similar-block__caption">
							{{ similarBlockCaption }}
						</div>
					</div>
					<v-row dense>
						<v-col
							v-for="book in similarToSelected"
							:key="book.id"
							cols="6"
							sm="4"
							md="3"
							lg="2"
						>
							<BookCard :book="book" />
						</v-col>
					</v-row>
				</section>
			</section>

			<section v-else class="empty">
				<v-icon size="56" color="grey">mdi-book-search-outline</v-icon>
				<div class="empty__title">
					Ничего не найдено по запросу «{{ debouncedQuery }}»
				</div>
				<div class="empty__hint">
					Проверьте правильность написания или попробуйте другой запрос
				</div>

				<div class="empty__chips">
					<v-chip
						v-for="q in popularQueries"
						:key="q"
						variant="tonal"
						class="ma-1"
						@click="runSearch(q)"
					>
						{{ q }}
					</v-chip>
				</div>
			</section>
		</v-container>
	</div>
</template>

<script>
import BookCard from '~/components/common/BookCard.vue';
import { mapBookList } from '~/utils/bookAdapters.js';

const MIN_QUERY_LENGTH   = 2;
const SEARCH_DEBOUNCE_MS = 300;
const SIMILAR_LIMIT      = 12;

// Несколько «затравочных» запросов для пустого экрана.
// На бэке отдельного эндпоинта «trending» пока нет — оставляем константой.
const POPULAR_QUERIES = [
	'Булгаков', 'Достоевский', 'Толкин',
	'Фантастика', 'Детектив', 'Классика',
	'Оруэлл', 'Фэнтези'
];

// Подсветка совпадения в подсказке. Экранирует HTML, regex и оборачивает в <mark>.
const highlightMatch = (text, query) => {
	const safe = String(text ?? '')
		.replace(/&/g, '&amp;')
		.replace(/</g, '&lt;')
		.replace(/>/g, '&gt;');
	if (!query) return safe;
	const escaped = String(query).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
	const regex = new RegExp(`(${escaped})`, 'gi');
	return safe.replace(regex, '<mark class="search-highlight">$1</mark>');
};

export default {
	name: 'SearchPage',

	components: { BookCard },

	setup() {
		const route = useRoute();
		const router = useRouter();
		const api = useApi();
		const initialQuery = (route.query.q || '').toString();
		return { route, router, api, initialQuery };
	},

	data() {
		return {
			popularQueries: POPULAR_QUERIES,

			searchInput:        this.initialQuery,
			debouncedQuery:     this.initialQuery,
			selectedSuggestion: null,

			isSearching: false,
			results: [],
			totalResults: 0,

			// Какую книгу пользователь выбрал из подсказок — она питает блок «Похожее».
			selectedBookId: null,
			selectedBookTitle: '',
			similarToSelected: [],

			suggestions: [],

			_debounceTimer: null
		};
	},

	computed: {
		hasQuery() {
			return !!(this.route.query.q && String(this.route.query.q).length >= MIN_QUERY_LENGTH);
		},

		similarBlockCaption() {
			if (!this.selectedBookTitle) return '';
			return `Подобрано по жанрам и темам книги «${this.selectedBookTitle}»`;
		}
	},

	watch: {
		// Реакция на смену ?q= в URL — заново тянем поиск.
		'route.query.q'(val) {
			const q = (val || '').toString();
			this.searchInput = q;
			this.debouncedQuery = q;
			this.selectedBookId = null;
			this.selectedBookTitle = '';
			this.similarToSelected = [];
			if (q) this.fetchResults(q);
		},

		// Дебаунс пользовательского ввода. Запускает подсказки и (если строка совпала с URL)
		// не дёргает поиск — поиск стартует только по выбору из подсказок или Enter.
		searchInput(val) {
			const next = (val || '').toString();
			if (!next) {
				this.suggestions = [];
				if (this._debounceTimer) clearTimeout(this._debounceTimer);
				return;
			}
			if (this._debounceTimer) clearTimeout(this._debounceTimer);
			this._debounceTimer = setTimeout(() => {
				this.debouncedQuery = next;
				this.fetchAutocomplete(next);
			}, SEARCH_DEBOUNCE_MS);
		}
	},

	async mounted() {
		// Если зашли по /search?q=... — сразу выполняем поиск.
		if (this.hasQuery) {
			await this.fetchResults(this.debouncedQuery);
		}
	},

	beforeUnmount() {
		if (this._debounceTimer) clearTimeout(this._debounceTimer);
	},

	methods: {
		highlight(text) {
			return highlightMatch(text, this.debouncedQuery);
		},

		// GET /api/search/autocomplete?query=...&limit=8
		async fetchAutocomplete(query) {
			if (!query || query.length < MIN_QUERY_LENGTH) {
				this.suggestions = [];
				return;
			}
			try {
				const res = await this.api.get('/api/search/autocomplete', {
					query: { query, limit: 8 }
				});
				this.suggestions = res?.suggestions || [];
			} catch {
				this.suggestions = [];
			}
		},

		// GET /api/search?query=...&page=1&pageSize=24
		async fetchResults(query) {
			this.isSearching = true;
			try {
				const res = await this.api.get('/api/search', {
					query: { query, page: 1, pageSize: 24, sortBy: 'relevance' }
				});
				this.results = mapBookList(res?.items);
				this.totalResults = res?.totalCount ?? this.results.length;
			} catch {
				this.results = [];
				this.totalResults = 0;
			} finally {
				this.isSearching = false;
			}

			// Если результат единственный — это и есть «искомая» книга.
			// Если пользователь до этого выбрал конкретную подсказку — берём её.
			if (this.selectedBookId) {
				await this.fetchSimilarFor(this.selectedBookId);
			} else if (this.results.length === 1) {
				const only = this.results[0];
				this.selectedBookId = only.id;
				this.selectedBookTitle = only.title;
				await this.fetchSimilarFor(only.id);
			} else {
				this.similarToSelected = [];
			}
		},

		// GET /api/books/{bookId}/recommendations?limit=12 — content-based блок снизу.
		async fetchSimilarFor(bookId) {
			try {
				const res = await this.api.get(
					`/api/books/${bookId}/recommendations`,
					{ query: { limit: SIMILAR_LIMIT } }
				);
				this.similarToSelected = mapBookList(res?.items);
			} catch {
				this.similarToSelected = [];
			}
		},

		runSearch(query) {
			const q = (query || '').toString().trim();
			if (q.length < MIN_QUERY_LENGTH) return;
			// Сбрасываем выбранную книгу — это новый запрос.
			this.selectedBookId = null;
			this.selectedBookTitle = '';
			this.router.push({ path: '/search', query: { q } });
		},

		onEnter() {
			this.runSearch(this.searchInput);
		},

		onSelectSuggestion(value) {
			if (!value) return;
			// SuggestionDto: { text, type ('book'|'author'|'genre'), id }
			if (value.type === 'book') {
				this.selectedBookId = value.id;
				this.selectedBookTitle = value.text;
			} else {
				this.selectedBookId = null;
				this.selectedBookTitle = '';
			}
			this.runSearch(value.text || '');
		},

		resetSearch() {
			this.searchInput = '';
			this.debouncedQuery = '';
			this.selectedSuggestion = null;
			this.selectedBookId = null;
			this.selectedBookTitle = '';
			this.results = [];
			this.totalResults = 0;
			this.similarToSelected = [];
			if (this.route.query.q) {
				this.router.push({ path: '/search' });
			}
		},

		suggestionIcon(type) {
			return {
				book:   'mdi-book-outline',
				author: 'mdi-account-outline',
				genre:  'mdi-shape-outline'
			}[type] || 'mdi-magnify';
		},

		suggestionTypeLabel(type) {
			return {
				book:   'название',
				author: 'автор',
				genre:  'жанр'
			}[type] || '';
		}
	}
};
</script>

<style scoped lang="scss">
.search-page {
	color: #fff;
	padding-top: 32px;
	padding-bottom: 80px;
	min-height: calc(100vh - 64px);

	&__container {
		max-width: 1400px;
		margin: 0 auto;
		padding: 0 40px;

		@media (max-width: 600px) { padding: 0 16px; }
	}

	&__heading {
		font-size: 56px;
		font-weight: 800;
		letter-spacing: 0.3px;
		line-height: 1;
		margin-bottom: 8px;

		@media (max-width: 600px) { font-size: 36px; }
	}

	&__lead {
		font-size: 14px;
		color: rgba(255, 255, 255, 0.6);
		margin-bottom: 28px;
		max-width: 640px;
	}

	&__search-row {
		margin-top: 12px;
		margin-bottom: 24px;
	}

	&__input {
		:deep(.v-field) {
			background: rgba(255, 255, 255, 0.05);
			border-radius: 999px;
			padding-inline: 12px;
		}
		:deep(.v-field__input) {
			min-height: 60px;
			font-size: 16px;
		}
	}
}

.suggestion-row {
	display: flex;
	flex-direction: column;
	min-width: 0;

	&__label {
		font-size: 14px;
		font-weight: 600;
		color: #fff;
	}

	&__type {
		font-size: 10px;
		text-transform: uppercase;
		letter-spacing: 0.5px;
		color: rgba(255, 255, 255, 0.45);
		padding: 2px 6px;
		border: 1px solid rgba(255, 255, 255, 0.1);
		border-radius: 999px;
		margin-left: 8px;
	}
}

.popular {
	margin-top: 12px;

	&__title {
		font-size: 12px;
		text-transform: uppercase;
		letter-spacing: 0.6px;
		color: rgba(255, 255, 255, 0.5);
		margin-bottom: 12px;
	}

	&__chips {
		display: flex;
		flex-wrap: wrap;
		gap: 8px;
	}

	&__chip { cursor: pointer; }
}

.results {
	&__header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		flex-wrap: wrap;
		gap: 12px;
		margin-bottom: 24px;
	}

	&__summary {
		font-size: 15px;
		color: rgba(255, 255, 255, 0.75);
	}

	&__skeleton {
		background: rgba(255, 255, 255, 0.04) !important;
		border-radius: 14px;
		aspect-ratio: 2 / 3;
	}
}

.similar-block {
	margin-top: 56px;
	padding-top: 32px;
	border-top: 1px solid rgba(255, 255, 255, 0.08);

	&__heading { margin-bottom: 20px; }

	&__title {
		font-size: 28px;
		font-weight: 800;
		letter-spacing: 0.2px;
		line-height: 1.15;

		@media (max-width: 600px) { font-size: 22px; }
	}

	&__caption {
		margin-top: 4px;
		font-size: 13px;
		color: rgba(255, 255, 255, 0.55);
	}
}

.empty {
	text-align: center;
	padding: 56px 16px;

	&__title {
		font-size: 18px;
		font-weight: 700;
		margin-top: 12px;
	}

	&__hint {
		font-size: 13px;
		color: rgba(255, 255, 255, 0.55);
		margin-top: 4px;
		margin-bottom: 20px;
	}

	&__chips {
		display: flex;
		flex-wrap: wrap;
		justify-content: center;
	}
}
</style>

<style>
.search-highlight {
	background-color: rgba(245, 210, 107, 0.25);
	color: inherit;
	border-radius: 2px;
	padding: 0 2px;
	font-weight: 600;
}
</style>
