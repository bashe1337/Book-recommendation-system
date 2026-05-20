<template>
	<div class="catalog-page">
		<v-container class="catalog-page__container" fluid>
			<h1 class="catalog-page__heading">Каталог книг</h1>

			<div class="catalog-page__layout">
				<!--
					ЛЕВАЯ КОЛОНКА — боковая панель фильтров.
					На десктопе занимает фиксированную ширину ~280px.
					На мобильных (< 960px) скрывается и открывается
					через v-navigation-drawer (см. ниже).
				-->
				<aside class="catalog-page__sidebar d-none d-md-block">
					<CatalogFilters
						:genres="mockGenres"
						:authors="mockAuthors"
						:languages="mockLanguages"
						@apply="applyFilters"
						@reset="resetFilters"
					/>
				</aside>

				<!-- ПРАВАЯ КОЛОНКА — toolbar + список книг -->
				<section class="catalog-page__main">
					<!--
						Toolbar: кнопка «Фильтры» на мобильном, поиск с
						автодополнением, селектор сортировки.
						На десктопе кнопка «Фильтры» спрятана (фильтры уже видны слева).
					-->
					<div class="catalog-page__toolbar">
						<v-btn
							variant="tonal"
							prepend-icon="mdi-filter-variant"
							class="catalog-page__filters-btn d-md-none"
							@click="filtersDrawer = true"
						>
							Фильтры
						</v-btn>

						<!--
							v-autocomplete с подсказками по mock-данным.
							no-filter=true — фильтрацию делаем сами на стороне родителя
							через getSuggestions (имитация триграммного поиска).
						-->
						<v-autocomplete
							v-model="selectedSuggestion"
							v-model:search="searchQuery"
							:items="suggestions"
							item-title="label"
							item-value="id"
							placeholder="Поиск книги или автора..."
							prepend-inner-icon="mdi-magnify"
							variant="outlined"
							density="comfortable"
							hide-details
							hide-no-data
							clearable
							no-filter
							return-object
							class="catalog-page__search"
							@update:model-value="onSelectSuggestion"
							@click:clear="clearSearch"
						/>

						<!-- Селектор сортировки -->
						<v-select
							v-model="sortBy"
							:items="sortOptions"
							item-title="title"
							item-value="value"
							density="comfortable"
							variant="outlined"
							hide-details
							class="catalog-page__sort"
						/>
					</div>

					<!--
						Список книг сгруппирован по жанру.
						Каждая группа: заголовок жанра с разделителем-линией
						и адаптивная сетка карточек.
					-->
					<template v-if="hasResults">
						<div
							v-for="(books, genre) in booksByGenre"
							:key="genre"
							class="catalog-page__group"
						>
							<div class="catalog-page__group-header">
								<h2 class="catalog-page__group-title">{{ genre }}</h2>
								<span class="catalog-page__group-count">{{ books.length }}</span>
								<div class="catalog-page__group-line" />
							</div>

							<v-row dense>
								<v-col
									v-for="book in books"
									:key="book.id"
									cols="12"
									sm="6"
									md="4"
									lg="3"
								>
									<BookCard :book="book" />
								</v-col>
							</v-row>
						</div>
					</template>

					<!-- Пустое состояние, если фильтры/поиск ничего не нашли -->
					<div v-else class="catalog-page__empty">
						<v-icon size="48" color="grey">mdi-book-search-outline</v-icon>
						<div class="text-h6 mt-2">Ничего не нашлось</div>
						<div class="text-caption text-grey">
							Попробуйте изменить фильтры или поисковый запрос
						</div>
					</div>
				</section>
			</div>
		</v-container>

		<!--
			Мобильный drawer с фильтрами.
			location="left" + temporary — выезжает поверх контента/затемняет фон.
			По требованию ТЗ — НЕ поверх контента в смысле «перекрывая в потоке»,
			а отдельным слоем drawer.
		-->
		<v-navigation-drawer
			v-model="filtersDrawer"
			location="left"
			temporary
			width="320"
			class="catalog-page__drawer d-md-none"
		>
			<CatalogFilters
				:genres="mockGenres"
				:authors="mockAuthors"
				:languages="mockLanguages"
				@apply="onMobileApply"
				@reset="resetFilters"
			/>
		</v-navigation-drawer>
	</div>
</template>

<script>
import CatalogFilters from '~/components/catalog/CatalogFilters.vue';
import BookCard from '~/components/common/BookCard.vue';

// === MOCK DATA — убрать, когда бэкенд будет готов ===
import {
	mockBooks,
	mockGenres,
	mockAuthors,
	mockLanguages,
	YEAR_MIN,
	YEAR_MAX,
	getSuggestions
} from '~/mocks/catalogMocks.js';

// TODO: подключить, когда бэкенд будет готов.
// eslint-disable-next-line no-unused-vars
const API_ENDPOINTS = {
	books:   '/api/books',          // список с фильтрами/сортировкой
	search:  '/api/books/search',   // поиск с подсказками (pg_trgm)
	filters: '/api/books/filters'   // справочники жанров/авторов/языков
};

export default {
	name: 'CatalogPage',

	components: { CatalogFilters, BookCard },

	data() {
		return {
			mockGenres,
			mockAuthors,
			mockLanguages,

			// Состояние drawer-а на мобильном.
			filtersDrawer: false,

			// Активные применённые фильтры (после клика «Применить»).
			// Дефолт = «всё разрешено». Структура совпадает с тем, что
			// эмитит CatalogFilters.
			activeFilters: {
				genres: [],
				authors: [],
				yearFrom: YEAR_MIN,
				yearTo:   YEAR_MAX,
				languages: []
			},

			// Поиск с автодополнением.
			searchQuery: '',
			selectedSuggestion: null,
			// Активный поисковый фильтр — он применяется к списку.
			// Отделён от searchQuery, чтобы изменение поля ввода
			// не сразу фильтровало книги, а только при выборе подсказки.
			activeSearch: null,

			sortBy: 'rating',
			sortOptions: [
				{ title: 'По рейтингу',         value: 'rating' },
				{ title: 'По дате (новые)',     value: 'date' },
				{ title: 'По популярности',     value: 'popularity' }
			]
		};
	},

	computed: {
		// Текущие подсказки для v-autocomplete.
		// Имитация триграммного поиска — substring match.
		suggestions() {
			return getSuggestions(this.searchQuery || '');
		},

		// Полный pipeline: фильтры -> поиск -> сортировка.
		// Реактивен по всем входам, поэтому работает без лишних watch.
		filteredBooks() {
			let result = [...mockBooks];

			if (this.activeFilters.genres.length) {
				result = result.filter((b) =>
					this.activeFilters.genres.includes(b.genre)
				);
			}

			if (this.activeFilters.authors.length) {
				result = result.filter((b) =>
					this.activeFilters.authors.includes(b.author)
				);
			}

			// Диапазон лет — фильтр всегда активен, диапазон по умолчанию
			// покрывает все валидные годы.
			result = result.filter(
				(b) =>
					b.year >= this.activeFilters.yearFrom &&
					b.year <= this.activeFilters.yearTo
			);

			if (this.activeFilters.languages.length) {
				result = result.filter((b) =>
					this.activeFilters.languages.includes(b.lang)
				);
			}

			// Применяем поисковый запрос: substring по title и author.
			if (this.activeSearch) {
				const q = this.activeSearch.toLowerCase();
				result = result.filter(
					(b) =>
						b.title.toLowerCase().includes(q) ||
						b.author.toLowerCase().includes(q)
				);
			}

			// Сортировка.
			if (this.sortBy === 'rating') {
				result.sort((a, b) => b.rating - a.rating);
			} else if (this.sortBy === 'date') {
				result.sort((a, b) => b.year - a.year);
			} else if (this.sortBy === 'popularity') {
				// TODO: заменить на поле b.popularity, когда бэкенд начнёт
				//       его отдавать. Пока ранжируем как rating, чтобы
				//       UX не ломался.
				result.sort((a, b) => b.rating - a.rating);
			}

			return result;
		},

		// Группировка для отображения секциями по жанрам.
		// Ключ — название жанра, значение — массив книг.
		booksByGenre() {
			const map = {};
			for (const book of this.filteredBooks) {
				if (!map[book.genre]) map[book.genre] = [];
				map[book.genre].push(book);
			}
			return map;
		},

		hasResults() {
			return this.filteredBooks.length > 0;
		}
	},

	methods: {
		applyFilters(filters) {
			// TODO: при подключении бэкенда — собрать query и вызвать
			//       GET /api/books?genre=...&author=...&yearFrom=...&yearTo=...&lang=...
			this.activeFilters = { ...filters };
		},

		resetFilters() {
			this.activeFilters = {
				genres: [],
				authors: [],
				yearFrom: YEAR_MIN,
				yearTo: YEAR_MAX,
				languages: []
			};
		},

		// На мобильном после «Применить» закрываем drawer, чтобы
		// пользователь сразу увидел отфильтрованный список.
		onMobileApply(filters) {
			this.applyFilters(filters);
			this.filtersDrawer = false;
		},

		// v-autocomplete отдал нам объект подсказки (return-object).
		// Берём из него title как поисковую фразу — это удобнее, чем
		// «Название — Автор», и при этом найдётся та же книга.
		onSelectSuggestion(value) {
			if (!value) {
				this.activeSearch = null;
				return;
			}
			this.activeSearch = value.title || '';
		},

		clearSearch() {
			this.searchQuery = '';
			this.selectedSuggestion = null;
			this.activeSearch = null;
		}
	}
};
</script>

<style scoped lang="scss">
.catalog-page {
	padding-top: 24px;
	padding-bottom: 60px;
	color: #fff;

	&__container {
		max-width: 1440px;
		margin: 0 auto;
		padding: 0 24px;

		@media (min-width: 1280px) {
			padding: 0 40px;
		}
	}

	&__heading {
		font-size: 28px;
		font-weight: 800;
		letter-spacing: 0.3px;
		margin-bottom: 24px;
	}

	// Двухколоночный layout. На мобильном вырождается в одну колонку,
	// т.к. сайдбар скрыт через d-none d-md-block.
	&__layout {
		display: grid;
		grid-template-columns: 280px 1fr;
		gap: 32px;

		@media (max-width: 960px) {
			grid-template-columns: 1fr;
			gap: 0;
		}
	}

	&__sidebar {
		position: sticky;
		top: 96px;
		// Если фильтров много — даём панели скроллиться, чтобы она
		// не уезжала за подвал на низких экранах.
		max-height: calc(100vh - 120px);
		overflow-y: auto;
	}

	&__main {
		min-width: 0; // важно для grid: иначе длинные строки распирают колонку
	}

	&__toolbar {
		display: flex;
		align-items: center;
		gap: 12px;
		margin-bottom: 32px;
		flex-wrap: wrap;
	}

	&__filters-btn {
		text-transform: none;
	}

	&__search {
		flex: 1 1 280px;
		min-width: 220px;
	}

	&__sort {
		width: 220px;

		@media (max-width: 600px) {
			width: 100%;
		}
	}

	&__group {
		margin-bottom: 40px;
	}

	&__group-header {
		display: flex;
		align-items: center;
		gap: 12px;
		margin-bottom: 16px;
	}

	&__group-title {
		font-size: 20px;
		font-weight: 700;
		letter-spacing: 0.3px;
	}

	&__group-count {
		font-size: 13px;
		color: rgba(255, 255, 255, 0.5);
		background: rgba(255, 255, 255, 0.08);
		border-radius: 999px;
		padding: 2px 10px;
	}

	// Разделитель-линия после заголовка жанра, занимает всё оставшееся место.
	&__group-line {
		flex: 1;
		height: 1px;
		background: rgba(255, 255, 255, 0.08);
	}

	&__empty {
		text-align: center;
		padding: 80px 16px;
		color: rgba(255, 255, 255, 0.65);
	}

	&__drawer {
		// Внутри drawer фильтры не должны иметь дополнительный фон/тень,
		// у самого drawer-а уже есть собственный фон Vuetify.
		:deep(.catalog-filters) {
			background: transparent !important;
			border: none !important;
			box-shadow: none !important;
			border-radius: 0 !important;
		}
	}
}
</style>
