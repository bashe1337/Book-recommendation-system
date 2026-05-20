<template>
	<div class="search-page" :class="{ 'search-page--initial': !hasQuery }">
		<v-container class="search-page__container">
			<!--
				========================================================
				ЗОНА 1 — строка поиска.
				На начальном экране (нет ?q=...) выровнена по центру и крупнее.
				При активном поиске — обычная по высоте, прижата к верху.
				========================================================
			-->
			<div v-if="!hasQuery" class="search-page__intro">
				<h1 class="search-page__intro-title">Найдите вашу следующую книгу</h1>
				<div class="search-page__intro-sub">
					Поиск по названию, автору или жанру — с подсказками и исправлением опечаток
				</div>
			</div>

			<div
				class="search-page__search-row"
				:class="{ 'search-page__search-row--big': !hasQuery }"
			>
				<!--
					v-autocomplete с кастомным item-slot.
					- v-model:search — текущий ввод (без debounce).
					- suggestions пересчитываются с задержкой 300мс
					  через debouncedQuery (см. watcher).
					- no-filter — отключаем встроенную фильтрацию;
					  список items уже отфильтрован нашим getSuggestions.
				-->
				<v-autocomplete
					v-model="selectedSuggestion"
					v-model:search="searchInput"
					:items="suggestions"
					item-title="label"
					item-value="id"
					:placeholder="!hasQuery ? 'Введите название, автора или жанр...' : 'Поиск...'"
					prepend-inner-icon="mdi-magnify"
					variant="outlined"
					:density="!hasQuery ? 'comfortable' : 'comfortable'"
					hide-details
					hide-no-data
					clearable
					no-filter
					return-object
					class="search-page__search-field"
					:class="{ 'search-page__search-field--big': !hasQuery }"
					@keyup.enter="onEnter"
					@update:model-value="onSelectSuggestion"
					@click:clear="resetSearch"
				>
					<!--
						Кастомная подсказка: иконка по типу (название/автор/жанр)
						+ подсвеченный label + sublabel + тип-чип.
					-->
					<template #item="{ item, props }">
						<v-list-item v-bind="props" :title="undefined">
							<template #prepend>
								<v-icon size="18" color="grey">{{ suggestionIcon(item.raw.type) }}</v-icon>
							</template>
							<div class="suggestion-row">
								<div
									class="suggestion-row__label"
									v-html="highlightMatch(item.raw.label, debouncedQuery)"
								/>
								<div
									class="suggestion-row__sublabel"
									v-html="highlightMatch(item.raw.sublabel, debouncedQuery)"
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

			<!--
				========================================================
				ЗОНА 2 — альтернативные запросы (query expansion).
				Показываем когда уже есть запрос. Текст-подзаголовок
				адаптируется под ситуацию: мало результатов / нет /
				просто похожие.
				========================================================
			-->
			<div v-if="hasQuery && expansions.length" class="expansions">
				<div class="expansions__title">{{ expansionsTitle }}</div>
				<div class="expansions__chips">
					<v-chip
						v-for="alt in expansions"
						:key="alt"
						variant="tonal"
						size="small"
						class="expansions__chip"
						@click="runSearch(alt)"
					>
						{{ alt }}
					</v-chip>
				</div>
			</div>

			<!--
				========================================================
				ЗОНА 3 — состояния результатов.
				4 состояния: начальный экран / loading skeleton /
				пустой результат / список.
				========================================================
			-->

			<!-- Начальный экран — популярные запросы -->
			<section v-if="!hasQuery" class="popular">
				<div class="popular__title">Популярные запросы</div>
				<div class="popular__chips">
					<v-chip
						v-for="q in mockPopularQueries"
						:key="q"
						variant="outlined"
						class="popular__chip"
						@click="runSearch(q)"
					>
						{{ q }}
					</v-chip>
				</div>
			</section>

			<!-- Skeleton-состояние: пока действует debounce ввода -->
			<section v-else-if="isSearching" class="results">
				<v-skeleton-loader
					v-for="i in 4"
					:key="i"
					type="list-item-avatar-three-line"
					class="results__skeleton"
				/>
			</section>

			<!-- Список результатов -->
			<section v-else-if="results.length" class="results">
				<div class="results__header">
					<div class="results__summary">
						Найдено <b>{{ results.length }}</b>
						{{ pluralizeBooks(results.length) }} по запросу
						«<span class="results__query">{{ debouncedQuery }}</span>»
					</div>

					<v-select
						v-model="sortBy"
						:items="sortOptions"
						item-title="title"
						item-value="value"
						density="compact"
						variant="outlined"
						hide-details
						class="results__sort"
					/>
				</div>

				<div class="results__list">
					<NuxtLink
						v-for="book in sortedResults"
						:key="book.id"
						:to="`/book/${book.id}`"
						class="result-card"
					>
						<v-img
							:src="book.cover"
							:aspect-ratio="2/3"
							cover
							class="result-card__cover"
						/>
						<div class="result-card__body">
							<h3
								class="result-card__title"
								v-html="highlightMatch(book.title, debouncedQuery)"
							/>
							<div
								class="result-card__author"
								v-html="highlightMatch(book.author, debouncedQuery)"
							/>

							<div class="result-card__chips">
								<v-chip size="x-small" class="mr-1">{{ book.genre }}</v-chip>
								<span class="result-card__meta">
									{{ book.year }} · {{ book.lang }}
								</span>
							</div>

							<p
								class="result-card__description"
								v-html="highlightMatch(book.description, debouncedQuery)"
							/>
						</div>

						<div class="result-card__rating">
							<v-icon size="16" color="amber">mdi-star</v-icon>
							<span>{{ book.rating.toFixed(1) }}</span>
						</div>
					</NuxtLink>
				</div>
			</section>

			<!-- Пустое состояние -->
			<section v-else class="empty">
				<v-icon size="56" color="grey">mdi-book-search-outline</v-icon>
				<div class="empty__title">
					Ничего не найдено по запросу «{{ debouncedQuery }}»
				</div>
				<div class="empty__hint">
					Проверьте правильность написания или попробуйте другой запрос
				</div>

				<!--
					По ТЗ: в пустом состоянии блок альтернативных запросов
					обязателен. Если основной expansions выше тоже пуст
					(нет даже жанров для fallback) — показываем популярные.
				-->
				<div class="empty__chips" v-if="emptyExpansions.length">
					<v-chip
						v-for="alt in emptyExpansions"
						:key="alt"
						variant="tonal"
						class="ma-1"
						@click="runSearch(alt)"
					>
						{{ alt }}
					</v-chip>
				</div>
			</section>
		</v-container>
	</div>
</template>

<script>
// === MOCK DATA — убрать, когда бэкенд будет готов ===
import {
	mockPopularQueries,
	getSuggestions,
	performSearch,
	getExpansions,
	highlightMatch
} from '~/mocks/searchMocks.js';

// Минимальная длина запроса для запуска поиска/подсказок — 2 символа,
// согласно ТЗ. Тот же порог использует getSuggestions внутри.
const MIN_QUERY_LENGTH = 2;

// Debounce для подсказок и поиска. ТЗ требует 300мс.
const SEARCH_DEBOUNCE_MS = 300;

export default {
	name: 'SearchPage',

	setup() {
		const route = useRoute();
		const router = useRouter();
		// Изначально берём q из URL — это позволяет открывать ссылку
		// /search?q=... и сразу видеть результат.
		const initialQuery = (route.query.q || '').toString();
		return { route, router, initialQuery };
	},

	data() {
		return {
			mockPopularQueries,

			// Ввод в поле — обновляется на каждое нажатие клавиши.
			searchInput: this.initialQuery,

			// Дебаунсенная копия — именно она используется для поиска,
			// подсказок и подсветки. Защищает от перерасчётов на каждый символ.
			debouncedQuery: this.initialQuery,

			// Выбранная подсказка (объект из v-autocomplete return-object).
			selectedSuggestion: null,

			// «Идёт поиск» — пока действует debounce между keystroke и
			// обновлением debouncedQuery. Используем для skeleton-состояния.
			isSearching: false,

			sortBy: 'relevance',
			sortOptions: [
				{ title: 'По релевантности', value: 'relevance' },
				{ title: 'По рейтингу',      value: 'rating' },
				{ title: 'По году',          value: 'year' }
			],

			// Таймер debounce; не делаем data-reactive — хранится для clearTimeout.
			_debounceTimer: null
		};
	},

	computed: {
		// Есть ли активный запрос в URL — это решает, что показывать:
		// начальный экран или зону результатов.
		hasQuery() {
			return !!(this.route.query.q && String(this.route.query.q).length >= MIN_QUERY_LENGTH);
		},

		// Подсказки для автокомплита.
		// Используем debouncedQuery, а не searchInput, — иначе список
		// дёргался бы на каждый символ.
		suggestions() {
			return getSuggestions(this.debouncedQuery);
		},

		// Результаты поиска (без сортировки) для активного запроса.
		results() {
			if (!this.hasQuery) return [];
			return performSearch(this.debouncedQuery);
		},

		// Сортировка по выбранному критерию.
		// Релевантность уже учтена в performSearch — здесь только overrides.
		sortedResults() {
			const list = [...this.results];
			if (this.sortBy === 'rating') list.sort((a, b) => b.rating - a.rating);
			else if (this.sortBy === 'year') list.sort((a, b) => b.year - a.year);
			return list;
		},

		// Заголовок над блоком альтернативных запросов адаптируется:
		// меньше 3 результатов — «Мало результатов, попробуйте также:»
		// если результатов нет — отдельный текст «Возможно, вы искали:»
		// иначе — обычное «Похожие запросы:»
		expansionsTitle() {
			if (!this.results.length)       return 'Возможно, вы искали:';
			if (this.results.length < 3)    return 'Мало результатов, попробуйте также:';
			return 'Похожие запросы:';
		},

		expansions() {
			return getExpansions(this.debouncedQuery, this.results);
		},

		// Для пустого состояния — гарантированно непустой набор:
		// если expansions пуст, fallback на популярные запросы.
		emptyExpansions() {
			return this.expansions.length ? this.expansions : this.mockPopularQueries;
		}
	},

	watch: {
		// Реакция на изменение URL: открыли /search?q=другое
		// (например, кликом на чип альтернативного запроса).
		'route.query.q'(val) {
			const q = (val || '').toString();
			this.searchInput = q;
			this.debouncedQuery = q;
			this.isSearching = false;
		},

		// Главный debounce: запрос/подсказки обновляются с задержкой
		// 300мс относительно последнего нажатия клавиши.
		searchInput(val) {
			const next = (val || '').toString();

			// Если пользователь полностью очистил поле руками — не ждём
			// дебаунса, чтобы skeleton не мигал.
			if (!next) {
				this.isSearching = false;
				this.debouncedQuery = '';
				if (this._debounceTimer) clearTimeout(this._debounceTimer);
				return;
			}

			this.isSearching = true;
			if (this._debounceTimer) clearTimeout(this._debounceTimer);
			this._debounceTimer = setTimeout(() => {
				this.debouncedQuery = next;
				this.isSearching = false;
			}, SEARCH_DEBOUNCE_MS);
		}
	},

	beforeUnmount() {
		if (this._debounceTimer) clearTimeout(this._debounceTimer);
	},

	methods: {
		highlightMatch,

		// Запуск поиска: обновляем ?q= в URL — это триггерит watcher
		// route.query.q и пересчитывает results/expansions.
		// TODO: при подключении бэкенда — здесь же дёргать
		//       GET /api/books/search?q=...&sort=...
		runSearch(query) {
			const q = (query || '').toString().trim();
			if (q.length < MIN_QUERY_LENGTH) return;
			this.router.push({ path: '/search', query: { q } });
		},

		onEnter() {
			this.runSearch(this.searchInput);
		},

		// Выбор подсказки в v-autocomplete: запускаем поиск по её label.
		// Сохраняем title книги, а не объект — он понятнее в URL.
		onSelectSuggestion(value) {
			if (!value) return;
			const q = value.label || value.title || value.toString();
			this.runSearch(q);
		},

		// Кнопка очистки: возвращаем пользователя к начальному экрану.
		resetSearch() {
			this.searchInput = '';
			this.debouncedQuery = '';
			this.selectedSuggestion = null;
			this.isSearching = false;
			if (this.route.query.q) {
				this.router.push({ path: '/search' });
			}
		},

		suggestionIcon(type) {
			return {
				title:  'mdi-book-outline',
				author: 'mdi-account-outline',
				genre:  'mdi-shape-outline'
			}[type] || 'mdi-magnify';
		},

		suggestionTypeLabel(type) {
			return {
				title:  'название',
				author: 'автор',
				genre:  'жанр'
			}[type] || '';
		},

		// Простое склонение «книга/книги/книг» — без библиотек.
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
.search-page {
	color: #fff;
	padding-top: 24px;
	padding-bottom: 60px;
	min-height: calc(100vh - 64px);

	&__container {
		max-width: 980px;
		margin: 0 auto;
		padding: 0 24px;
	}

	// Начальный экран — больше воздуха сверху.
	&--initial {
		padding-top: 60px;
	}

	&__intro {
		text-align: center;
		margin-bottom: 24px;
	}

	&__intro-title {
		font-size: 36px;
		font-weight: 800;
		letter-spacing: 0.3px;

		@media (max-width: 600px) {
			font-size: 24px;
		}
	}

	&__intro-sub {
		margin-top: 8px;
		font-size: 14px;
		color: rgba(255, 255, 255, 0.6);
	}

	&__search-row {
		margin-bottom: 24px;

		&--big {
			max-width: 720px;
			margin: 0 auto 32px;
		}
	}

	// Чуть крупнее на начальном экране.
	&__search-field--big {
		:deep(.v-field) {
			font-size: 16px;
		}
		:deep(.v-field__input) {
			min-height: 56px;
		}
	}
}

// Подсказки в выпадающем списке.
.suggestion-row {
	display: flex;
	flex-direction: column;
	min-width: 0;

	&__label {
		font-size: 14px;
		font-weight: 600;
		color: #fff;
	}

	&__sublabel {
		font-size: 12px;
		color: rgba(255, 255, 255, 0.55);
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

// Блок альтернативных запросов.
.expansions {
	margin-bottom: 20px;

	&__title {
		font-size: 13px;
		color: rgba(255, 255, 255, 0.65);
		margin-bottom: 8px;
	}

	&__chips {
		display: flex;
		flex-wrap: wrap;
		gap: 6px;
	}

	&__chip {
		cursor: pointer;
	}
}

// Популярные запросы на начальном экране.
.popular {
	max-width: 720px;
	margin: 0 auto;
	text-align: center;

	&__title {
		font-size: 13px;
		text-transform: uppercase;
		letter-spacing: 0.6px;
		color: rgba(255, 255, 255, 0.5);
		margin-bottom: 12px;
	}

	&__chips {
		display: flex;
		flex-wrap: wrap;
		justify-content: center;
		gap: 8px;
	}

	&__chip {
		cursor: pointer;
	}
}

// Результаты поиска.
.results {
	&__header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		flex-wrap: wrap;
		gap: 12px;
		margin-bottom: 16px;
	}

	&__summary {
		font-size: 14px;
		color: rgba(255, 255, 255, 0.75);
	}

	&__query {
		color: #fff;
		font-weight: 600;
	}

	&__sort {
		width: 220px;

		@media (max-width: 600px) {
			width: 100%;
		}
	}

	&__list {
		display: flex;
		flex-direction: column;
		gap: 12px;
	}

	&__skeleton {
		background: rgba(255, 255, 255, 0.04) !important;
		border-radius: 14px;
		margin-bottom: 12px;
	}
}

// Карточка результата поиска (горизонтальная).
.result-card {
	display: grid;
	grid-template-columns: 80px 1fr auto;
	gap: 16px;
	padding: 12px;
	background: rgba(255, 255, 255, 0.04);
	border: 1px solid rgba(255, 255, 255, 0.06);
	border-radius: 14px;
	color: #fff;
	text-decoration: none;
	transition: background 0.2s ease, transform 0.2s ease;

	&:hover {
		background: rgba(255, 255, 255, 0.07);
		transform: translateY(-1px);
	}

	&__cover {
		width: 80px;
		height: 120px;
		border-radius: 8px;
		overflow: hidden;
		background: #2a2a2a;
	}

	&__body {
		min-width: 0;
	}

	&__title {
		font-size: 16px;
		font-weight: 700;
		line-height: 1.25;
		margin: 0 0 4px;
	}

	&__author {
		font-size: 13px;
		color: rgba(255, 255, 255, 0.7);
		margin-bottom: 6px;
	}

	&__chips {
		display: flex;
		align-items: center;
		gap: 8px;
		flex-wrap: wrap;
		margin-bottom: 6px;
	}

	&__meta {
		font-size: 12px;
		color: rgba(255, 255, 255, 0.5);
	}

	&__description {
		font-size: 13px;
		line-height: 1.5;
		color: rgba(255, 255, 255, 0.7);
		margin: 0;
		display: -webkit-box;
		-webkit-line-clamp: 2;
		-webkit-box-orient: vertical;
		overflow: hidden;
	}

	&__rating {
		display: flex;
		align-items: center;
		gap: 4px;
		font-weight: 700;
		font-size: 14px;
		color: rgba(255, 255, 255, 0.9);
		align-self: start;
	}

	@media (max-width: 600px) {
		grid-template-columns: 60px 1fr;
		grid-template-rows: auto auto;

		&__cover {
			width: 60px;
			height: 90px;
		}

		&__rating {
			grid-column: 2;
			margin-top: 4px;
		}
	}
}

// Пустое состояние.
.empty {
	text-align: center;
	padding: 40px 16px;

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

<!--
	Глобальный стиль для подсветки совпадений.
	Размещён без scoped, потому что HTML вставляется через v-html и
	в scoped-режиме селектор .search-highlight не цепляется к нему.
-->
<style>
.search-highlight {
	background-color: rgba(var(--v-theme-primary), 0.25);
	color: inherit;
	border-radius: 2px;
	padding: 0 2px;
	font-weight: 600;
}
</style>
