<template>
	<div class="catalog-page">
		<v-container class="catalog-page__container" fluid>
			<header class="catalog-page__header">
				<h1 class="catalog-page__title">Каталог</h1>
				<div class="catalog-page__subtitle">
					{{ totalBooks }} {{ pluralizeBooks(totalBooks) }}
					<span class="catalog-page__dot">·</span>
					обновлено сегодня
				</div>
			</header>

			<!-- Поиск по строке: триггерит ре-загрузку с задержкой 300мс. -->
			<div class="catalog-page__search">
				<v-text-field
					v-model="searchQuery"
					placeholder="Поиск по названию или автору..."
					prepend-inner-icon="mdi-magnify"
					variant="outlined"
					density="comfortable"
					hide-details
					clearable
					rounded="pill"
					class="catalog-page__search-input"
				/>
			</div>

			<!--
				Жанры: GET /api/genres → [{ id, name, slug }]
				Активный = выбранный фильтр (selectedGenreId), 'all' — без фильтра.
			-->
			<div class="catalog-page__filter-block">
				<div class="catalog-page__filter-label">Жанры</div>
				<div class="catalog-page__chips">
					<v-chip
						:variant="selectedGenreId === 'all' ? 'flat' : 'outlined'"
						:color="selectedGenreId === 'all' ? 'white' : 'grey-lighten-1'"
						class="catalog-page__chip"
						@click="selectedGenreId = 'all'"
					>
						Все жанры
					</v-chip>
					<v-chip
						v-for="g in genres"
						:key="g.id"
						:variant="selectedGenreId === g.id ? 'flat' : 'outlined'"
						:color="selectedGenreId === g.id ? 'white' : 'grey-lighten-1'"
						class="catalog-page__chip"
						@click="selectedGenreId = g.id"
					>
						{{ g.name }}
					</v-chip>
				</div>
			</div>

			<div class="catalog-page__results-bar">
				<div class="catalog-page__found">
					Найдено: <b>{{ totalBooks }}</b>
				</div>
				<div class="catalog-page__sort">
					<span class="catalog-page__sort-label">Сортировка:</span>
					<v-select
						v-model="sortBy"
						:items="sortOptions"
						item-title="title"
						item-value="value"
						density="compact"
						variant="outlined"
						hide-details
						class="catalog-page__sort-select"
					/>
				</div>
			</div>

			<div v-if="loading" class="text-center py-8">
				<v-progress-circular indeterminate color="primary" size="36" />
			</div>

			<template v-else-if="books.length">
				<v-row dense>
					<v-col
						v-for="book in books"
						:key="book.id"
						cols="6"
						sm="4"
						md="3"
						lg="2"
					>
						<BookCard :book="book" />
					</v-col>
				</v-row>

				<!-- Простая пагинация: «Показать ещё». -->
				<div v-if="hasMore" class="text-center mt-6">
					<v-btn
						variant="tonal"
						:loading="loadingMore"
						@click="loadMore"
					>
						Показать ещё
					</v-btn>
				</div>
			</template>

			<EmptyState
				v-else
				icon="mdi-book-search-outline"
				text="Ничего не нашлось. Попробуйте сбросить фильтры"
			/>
		</v-container>
	</div>
</template>

<script>
import BookCard from '~/components/common/BookCard.vue';
import EmptyState from '~/components/common/EmptyState.vue';
import { mapBookList } from '~/utils/bookAdapters.js';

// Дебаунс поискового запроса (мс).
const SEARCH_DEBOUNCE_MS = 300;
// Размер страницы для /api/books.
const PAGE_SIZE = 24;

export default {
	name: 'CatalogPage',

	components: { BookCard, EmptyState },

	setup() {
		const api = useApi();
		return { api };
	},

	data() {
		return {
			loading: true,
			loadingMore: false,

			searchQuery: '',
			selectedGenreId: 'all',
			sortBy: 'rating',
			sortOptions: [
				{ title: 'По рейтингу',         value: 'rating' },
				{ title: 'По дате (новые)',     value: 'date' },
				{ title: 'По популярности',     value: 'popularity' }
			],

			genres: [],

			page: 1,
			totalBooks: 0,
			totalPages: 1,
			books: [],

			_debounceTimer: null
		};
	},

	computed: {
		hasMore() {
			return this.page < this.totalPages;
		}
	},

	watch: {
		searchQuery() { this.debouncedReload(); },
		selectedGenreId() { this.reload(); },
		sortBy() { this.reload(); }
	},

	async mounted() {
		// Параллельно подгружаем список жанров и первую страницу книг.
		await Promise.allSettled([this.loadGenres(), this.reload()]);
	},

	beforeUnmount() {
		if (this._debounceTimer) clearTimeout(this._debounceTimer);
	},

	methods: {
		// GET /api/genres — единый справочник для чипов.
		async loadGenres() {
			try {
				const list = await this.api.get('/api/genres');
				this.genres = Array.isArray(list) ? list : [];
			} catch {
				this.genres = [];
			}
		},

		// Дебаунсенная перезагрузка для поиска по строке.
		debouncedReload() {
			if (this._debounceTimer) clearTimeout(this._debounceTimer);
			this._debounceTimer = setTimeout(() => this.reload(), SEARCH_DEBOUNCE_MS);
		},

		// GET /api/books с фильтрами и сортировкой.
		// Возвращает PagedResult<BookSummaryDto>.
		async reload() {
			this.page = 1;
			this.loading = true;
			try {
				const res = await this.api.get('/api/books', { query: this.buildQuery(1) });
				this.books = mapBookList(res?.items);
				this.totalBooks = res?.totalCount ?? this.books.length;
				this.totalPages = res?.totalPages ?? 1;
			} catch {
				this.books = [];
				this.totalBooks = 0;
				this.totalPages = 1;
			} finally {
				this.loading = false;
			}
		},

		async loadMore() {
			if (!this.hasMore || this.loadingMore) return;
			this.loadingMore = true;
			const nextPage = this.page + 1;
			try {
				const res = await this.api.get('/api/books', { query: this.buildQuery(nextPage) });
				const more = mapBookList(res?.items);
				this.books = [...this.books, ...more];
				this.page = nextPage;
				this.totalPages = res?.totalPages ?? this.totalPages;
			} catch {
				/* оставляем то, что уже было */
			} finally {
				this.loadingMore = false;
			}
		},

		// Сборка query-параметров под BookFilterRequest.
		buildQuery(page) {
			const q = {
				page,
				pageSize: PAGE_SIZE,
				sortBy: this.sortBy,
				sortDesc: true
			};
			if (this.searchQuery.trim()) q.search = this.searchQuery.trim();
			if (this.selectedGenreId !== 'all') {
				// Бэк ожидает массив Guid в GenreIds.
				q.genreIds = this.selectedGenreId;
			}
			return q;
		},

		pluralizeBooks(n) {
			const abs = Math.abs(n) % 100;
			const last = abs % 10;
			if (abs > 10 && abs < 20) return 'книг';
			if (last === 1) return 'книга';
			if (last >= 2 && last <= 4) return 'книги';
			return 'книг';
		}
	}
};
</script>

<style scoped lang="scss">
.catalog-page {
	color: #fff;
	padding-top: 32px;
	padding-bottom: 80px;

	&__container {
		max-width: 1440px;
		margin: 0 auto;
		padding: 0 40px;

		@media (max-width: 600px) { padding: 0 16px; }
	}

	&__header {
		margin-bottom: 24px;
	}

	&__title {
		font-size: 56px;
		font-weight: 800;
		letter-spacing: 0.3px;
		line-height: 1;

		@media (max-width: 600px) { font-size: 36px; }
	}

	&__subtitle {
		margin-top: 8px;
		font-size: 14px;
		color: rgba(255, 255, 255, 0.55);
	}

	&__dot {
		margin: 0 6px;
		opacity: 0.5;
	}

	&__search {
		margin-bottom: 28px;
	}

	&__search-input {
		:deep(.v-field) {
			background: rgba(255, 255, 255, 0.04);
			border-radius: 999px;
			padding-inline: 12px;
		}
		:deep(.v-field__input) {
			min-height: 56px;
			font-size: 15px;
		}
	}

	&__filter-block {
		margin-bottom: 20px;
	}

	&__filter-label {
		font-size: 11px;
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

	&__chip {
		cursor: pointer;
		font-weight: 600;
	}

	&__results-bar {
		display: flex;
		justify-content: space-between;
		align-items: center;
		flex-wrap: wrap;
		gap: 12px;
		margin-top: 24px;
		margin-bottom: 24px;
	}

	&__found {
		font-size: 14px;
		color: rgba(255, 255, 255, 0.75);
	}

	&__sort {
		display: flex;
		align-items: center;
		gap: 10px;
	}

	&__sort-label {
		font-size: 13px;
		color: rgba(255, 255, 255, 0.55);
	}

	&__sort-select {
		width: 200px;

		:deep(.v-field) { border-radius: 10px; }
		:deep(.v-field__outline__start),
		:deep(.v-field__outline__notch::before),
		:deep(.v-field__outline__notch::after),
		:deep(.v-field__outline__end) {
			border-color: rgb(var(--v-theme-primary)) !important;
		}

		@media (max-width: 600px) { width: 160px; }
	}
}
</style>
