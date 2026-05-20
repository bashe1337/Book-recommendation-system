<template>
	<!--
		Боковая панель фильтров каталога.
		Использует v-expansion-panels: каждая категория фильтра — отдельный
		раскрываемый блок. Все блоки развёрнуты по умолчанию (model = [0,1,2,3]),
		чтобы пользователь сразу видел доступные критерии.

		v-model на «filters» — это локальная копия. В родителя ничего не
		уходит до клика «Применить», чтобы пользователь мог собрать набор
		фильтров целиком и не дёргать список книг на каждое изменение.
	-->
	<v-card class="catalog-filters" elevation="0" border>
		<div class="catalog-filters__header">
			<v-icon size="20" start>mdi-filter-variant</v-icon>
			<span class="catalog-filters__title">Фильтры</span>
			<v-spacer />
			<!-- быстрый сброс прямо в шапке, если есть активные фильтры -->
			<v-btn
				v-if="hasActiveFilters"
				variant="text"
				size="small"
				color="error"
				@click="onReset"
			>
				Сбросить
			</v-btn>
		</div>

		<v-divider />

		<v-expansion-panels
			v-model="openPanels"
			multiple
			variant="accordion"
			class="catalog-filters__panels"
		>
			<!-- ===== Жанр ===== -->
			<v-expansion-panel value="genre">
				<v-expansion-panel-title>Жанр</v-expansion-panel-title>
				<v-expansion-panel-text>
					<!--
						По ТЗ показываем первые 6 жанров, остальные —
						под кнопкой «Показать ещё». visibleGenres ниже
						учитывает флаг showAllGenres.
					-->
					<v-checkbox
						v-for="g in visibleGenres"
						:key="g"
						v-model="filters.genres"
						:value="g"
						:label="g"
						hide-details
						density="compact"
						class="catalog-filters__checkbox"
					/>
					<v-btn
						v-if="genres.length > GENRE_PREVIEW_COUNT"
						variant="text"
						size="small"
						class="mt-1"
						@click="showAllGenres = !showAllGenres"
					>
						{{ showAllGenres ? 'Скрыть' : 'Показать ещё' }}
					</v-btn>
				</v-expansion-panel-text>
			</v-expansion-panel>

			<!-- ===== Автор ===== -->
			<v-expansion-panel value="author">
				<v-expansion-panel-title>Автор</v-expansion-panel-title>
				<v-expansion-panel-text>
					<v-text-field
						v-model="authorQuery"
						placeholder="Поиск автора..."
						prepend-inner-icon="mdi-magnify"
						density="compact"
						variant="outlined"
						hide-details
						class="mb-3"
					/>
					<div class="catalog-filters__authors-list">
						<v-checkbox
							v-for="a in filteredAuthors"
							:key="a"
							v-model="filters.authors"
							:value="a"
							:label="a"
							hide-details
							density="compact"
							class="catalog-filters__checkbox"
						/>
						<div
							v-if="!filteredAuthors.length"
							class="text-caption text-grey text-center py-2"
						>
							Авторы не найдены
						</div>
					</div>
				</v-expansion-panel-text>
			</v-expansion-panel>

			<!-- ===== Год издания ===== -->
			<v-expansion-panel value="year">
				<v-expansion-panel-title>Год издания</v-expansion-panel-title>
				<v-expansion-panel-text>
					<!--
						Range slider + два поля «от» и «до».
						Поля и слайдер связаны через v-model одного и того же
						filters.yearRange — поэтому правки в любом из них
						синхронно отражаются на другом.
					-->
					<v-range-slider
						v-model="filters.yearRange"
						:min="YEAR_MIN"
						:max="YEAR_MAX"
						:step="1"
						thumb-label
						hide-details
						class="catalog-filters__year-slider"
					/>
					<div class="d-flex align-center mt-2">
						<v-text-field
							:model-value="filters.yearRange[0]"
							label="от"
							type="number"
							density="compact"
							variant="outlined"
							hide-details
							@update:model-value="setYearFrom"
						/>
						<span class="mx-2 text-grey">—</span>
						<v-text-field
							:model-value="filters.yearRange[1]"
							label="до"
							type="number"
							density="compact"
							variant="outlined"
							hide-details
							@update:model-value="setYearTo"
						/>
					</div>
				</v-expansion-panel-text>
			</v-expansion-panel>

			<!-- ===== Язык ===== -->
			<v-expansion-panel value="lang">
				<v-expansion-panel-title>Язык</v-expansion-panel-title>
				<v-expansion-panel-text>
					<v-chip-group
						v-model="filters.languages"
						multiple
						column
						selected-class="catalog-filters__chip--selected"
					>
						<v-chip
							v-for="l in languages"
							:key="l"
							:value="l"
							variant="outlined"
							size="small"
						>
							{{ l }}
						</v-chip>
					</v-chip-group>
				</v-expansion-panel-text>
			</v-expansion-panel>
		</v-expansion-panels>

		<v-divider />

		<!-- Кнопки управления фильтрами -->
		<div class="catalog-filters__actions">
			<v-btn
				block
				color="primary"
				rounded="xl"
				size="large"
				@click="onApply"
			>
				Применить
			</v-btn>
			<v-btn
				block
				variant="text"
				size="small"
				class="mt-2"
				@click="onReset"
			>
				Сбросить
			</v-btn>
		</div>
	</v-card>
</template>

<script>
import { YEAR_MIN, YEAR_MAX } from '~/mocks/catalogMocks.js';

// Сколько жанров показываем до клика «Показать ещё».
const GENRE_PREVIEW_COUNT = 6;

// Дефолтное состояние фильтров. Вынесено в функцию, чтобы при сбросе
// получать новый чистый объект без рисков случайного шаринга ссылок.
const buildDefaultFilters = () => ({
	genres: [],
	authors: [],
	yearRange: [YEAR_MIN, YEAR_MAX],
	languages: []
});

export default {
	name: 'CatalogFilters',

	emits: ['apply', 'reset'],

	props: {
		// Списки-справочники приходят от родителя — он держит источник правды.
		genres:    { type: Array, default: () => [] },
		authors:   { type: Array, default: () => [] },
		languages: { type: Array, default: () => [] }
	},

	data() {
		return {
			YEAR_MIN,
			YEAR_MAX,
			GENRE_PREVIEW_COUNT,

			// все панели развёрнуты при инициализации
			openPanels: ['genre', 'author', 'year', 'lang'],

			showAllGenres: false,
			authorQuery: '',
			filters: buildDefaultFilters()
		};
	},

	computed: {
		// Усечённый список жанров для preview-режима.
		visibleGenres() {
			return this.showAllGenres
				? this.genres
				: this.genres.slice(0, GENRE_PREVIEW_COUNT);
		},

		// Фильтрация списка авторов по строке поиска.
		// case-insensitive подстрочный матч — имитация будущего pg_trgm.
		filteredAuthors() {
			if (!this.authorQuery.trim()) return this.authors;
			const q = this.authorQuery.trim().toLowerCase();
			return this.authors.filter((a) => a.toLowerCase().includes(q));
		},

		// Используется и для бейджа «Сбросить» в шапке.
		hasActiveFilters() {
			return (
				this.filters.genres.length > 0 ||
				this.filters.authors.length > 0 ||
				this.filters.languages.length > 0 ||
				this.filters.yearRange[0] !== YEAR_MIN ||
				this.filters.yearRange[1] !== YEAR_MAX
			);
		}
	},

	methods: {
		// Поля «от/до» приходят строками от v-text-field. Парсим, кладём
		// в правильный конец yearRange и кэпим в допустимых границах.
		setYearFrom(value) {
			const n = Number(value);
			if (!Number.isFinite(n)) return;
			const from = Math.max(YEAR_MIN, Math.min(n, this.filters.yearRange[1]));
			this.filters.yearRange = [from, this.filters.yearRange[1]];
		},
		setYearTo(value) {
			const n = Number(value);
			if (!Number.isFinite(n)) return;
			const to = Math.min(YEAR_MAX, Math.max(n, this.filters.yearRange[0]));
			this.filters.yearRange = [this.filters.yearRange[0], to];
		},

		onApply() {
			// Отдаём наружу неглубокую копию, чтобы родитель не мог
			// случайно поправить наше внутреннее состояние.
			// TODO: при подключении бэкенда — собирать query-параметры
			//       и вызывать GET /api/books?genre=...&author=...&yearFrom=...&yearTo=...&lang=...
			this.$emit('apply', {
				genres:    [...this.filters.genres],
				authors:   [...this.filters.authors],
				yearFrom:  this.filters.yearRange[0],
				yearTo:    this.filters.yearRange[1],
				languages: [...this.filters.languages]
			});
		},

		onReset() {
			this.filters = buildDefaultFilters();
			this.authorQuery = '';
			this.showAllGenres = false;
			this.$emit('reset');
		}
	}
};
</script>

<style scoped lang="scss">
.catalog-filters {
	background: rgba(0, 0, 0, 0.7) !important;
	border-radius: 24px !important;
	border: 1px solid rgba(255, 255, 255, 0.1);
	box-shadow: 0 10px 40px rgba(0, 0, 0, 0.4);
	backdrop-filter: saturate(180%) blur(5px);
	overflow: hidden;

	&__header {
		display: flex;
		align-items: center;
		padding: 16px 20px;
	}

	&__title {
		font-size: 18px;
		font-weight: 700;
		letter-spacing: 0.3px;
	}

	&__panels {
		// v-expansion-panels по умолчанию имеет белый фон; затемняем,
		// чтобы вписаться в общий «glass»-стиль панели.
		:deep(.v-expansion-panel) {
			background: transparent !important;
			color: #fff;
		}

		:deep(.v-expansion-panel-title) {
			font-weight: 600;
			font-size: 14px;
			letter-spacing: 0.3px;
			padding-block: 12px;
			min-height: 44px;
		}

		:deep(.v-expansion-panel-text__wrapper) {
			padding-inline: 16px;
		}
	}

	&__checkbox :deep(.v-label) {
		font-size: 14px;
		opacity: 0.9;
	}

	// Чтобы длинный список авторов не растягивал колонку на десктопе.
	&__authors-list {
		max-height: 220px;
		overflow-y: auto;
	}

	&__year-slider {
		margin-top: 8px;
	}

	&__chip--selected {
		background: rgba(255, 255, 255, 0.15) !important;
		color: #fff !important;
	}

	&__actions {
		padding: 16px;
	}
}
</style>
