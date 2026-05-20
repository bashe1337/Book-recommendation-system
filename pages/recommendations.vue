<template>
	<div class="rec-page">
		<v-container class="rec-page__container">
			<!--
				========================================================
				ВЕРХНЯЯ ЗОНА — заголовок + подзаголовок + сводка.
				========================================================
			-->
			<header class="rec-page__header">
				<h1 class="rec-page__title">Рекомендации для вас</h1>
				<div class="rec-page__subtitle">
					Подобрано специально для
					<b>{{ currentUserName }}</b>
					на основе вашей истории
				</div>

				<!--
					Цветные счётчики-чипы. Цифры из mockUserSummary
					(в реальности — из /api/profile/me).
				-->
				<div class="rec-page__summary">
					<v-chip color="primary"   variant="tonal" class="rec-page__summary-chip">
						<v-icon size="16" start>mdi-star-outline</v-icon>
						На основе {{ summary.ratingsCount }} оценок
					</v-chip>
					<v-chip color="success"   variant="tonal" class="rec-page__summary-chip">
						<v-icon size="16" start>mdi-shape-outline</v-icon>
						{{ summary.favoriteGenresCount }} любимых жанров
					</v-chip>
					<v-chip color="secondary" variant="tonal" class="rec-page__summary-chip">
						<v-icon size="16" start>mdi-history</v-icon>
						{{ summary.viewedBooksCount }} просмотренных книг
					</v-chip>
				</div>
			</header>

			<!--
				========================================================
				ОСНОВНОЙ LAYOUT — фильтры слева, сетка карточек справа.
				На <960px фильтры разворачиваются сверху как обычная карточка.
				========================================================
			-->
			<div class="rec-page__layout">
				<aside class="rec-page__filters">
					<v-card class="filters-card" elevation="0">
						<h2 class="filters-card__title">Фильтры</h2>

						<!-- Тип рекомендаций -->
						<div class="filters-card__section">
							<div class="filters-card__label">Тип рекомендаций</div>
							<v-radio-group
								v-model="filters.type"
								density="compact"
								hide-details
								class="filters-card__radios"
							>
								<v-radio value="all"           label="Все рекомендации" />
								<v-radio value="genre"         label="По любимым жанрам" />
								<v-radio value="similar"       label="Похожие на просмотренные" />
								<v-radio value="collaborative" label="Читают похожие на вас" />
							</v-radio-group>
						</div>

						<!-- Жанры из предпочтений пользователя -->
						<div class="filters-card__section">
							<div class="filters-card__label">Жанр</div>
							<v-checkbox
								v-for="g in userGenres"
								:key="g"
								v-model="filters.genres"
								:value="g"
								:label="g"
								density="compact"
								hide-details
								class="filters-card__checkbox"
							/>
						</div>

						<!-- Минимальный рейтинг -->
						<div class="filters-card__section">
							<div class="filters-card__label">
								Минимальный рейтинг:
								<b>{{ filters.minRating.toFixed(1) }}</b>
							</div>
							<v-slider
								v-model="filters.minRating"
								:min="1"
								:max="5"
								:step="0.5"
								thumb-label
								hide-details
								color="primary"
							/>
						</div>

						<!-- Скрыть прочитанные -->
						<div class="filters-card__section">
							<v-switch
								v-model="filters.excludeRead"
								color="primary"
								density="compact"
								hide-details
								label="Скрыть прочитанные"
							/>
						</div>

						<v-btn
							variant="outlined"
							size="small"
							block
							@click="resetFilters"
						>
							Сбросить фильтры
						</v-btn>
					</v-card>
				</aside>

				<!-- Сетка рекомендаций -->
				<section class="rec-page__results">
					<div class="rec-page__results-summary">
						Найдено <b>{{ filteredRecommendations.length }}</b>
						рекомендаций
					</div>

					<template v-if="visibleRecommendations.length">
						<v-row dense>
							<v-col
								v-for="book in visibleRecommendations"
								:key="book.id"
								cols="12"
								sm="6"
								md="4"
								lg="3"
							>
								<!--
									Карточка + бейдж причины рекомендации в углу.
									pointer-events: none на чипе — чтобы клик уходил
									в BookCard, а не «съедался» бейджем.
								-->
								<div class="rec-card">
									<BookCard :book="book" />
									<v-chip
										v-if="book.recommendationLabel"
										:color="badgeColor(book.recommendationType)"
										variant="flat"
										size="x-small"
										class="rec-card__badge"
									>
										{{ book.recommendationLabel }}
									</v-chip>
								</div>
							</v-col>
						</v-row>

						<div v-if="hasMore" class="text-center mt-6">
							<v-btn variant="tonal" @click="page++">
								Загрузить ещё
							</v-btn>
						</div>
					</template>

					<EmptyState
						v-else
						icon="mdi-book-search-outline"
						text="По выбранным фильтрам пока ничего не нашлось"
					/>
				</section>
			</div>
		</v-container>
	</div>
</template>

<script>
import { storeToRefs } from 'pinia';
import { useUserStore } from '~/stores/user';

import BookCard from '~/components/common/BookCard.vue';
import EmptyState from '~/components/common/EmptyState.vue';

// === MOCK DATA — убрать, когда бэкенд будет готов ===
import {
	mockRecommendations,
	mockUserSummary,
	RECOMMENDATION_BADGE_COLORS
} from '~/mocks/recommendationsMocks.js';

// Защита от анонимов: middleware «auth» редиректит на /login.
definePageMeta({ middleware: 'auth' });

const PAGE_SIZE = 8;

// Дефолт жанров для пользователя, у которого preferredGenres пуст —
// чтобы блок «Жанр» в фильтрах не оставался пустым.
const FALLBACK_GENRES = ['Классика', 'Фантастика', 'Фэнтези', 'Антиутопия'];

export default {
	name: 'RecommendationsPage',

	components: { BookCard, EmptyState },

	setup() {
		const userStore = useUserStore();
		const { user } = storeToRefs(userStore);
		return { user };
	},

	data() {
		return {
			summary: mockUserSummary,

			// Полный список рекомендаций — на бэке придёт от API.
			allRecommendations: mockRecommendations,

			// Активные фильтры.
			filters: {
				type: 'all',
				genres: [],
				minRating: 1,
				excludeRead: false
			},

			// Локальная пагинация через «Загрузить ещё».
			page: 1
		};
	},

	computed: {
		currentUserName() {
			return this.user?.name || 'вас';
		},

		// Список жанров для фильтра.
		// Берём предпочтения пользователя, иначе fallback на популярные.
		userGenres() {
			const list = this.user?.preferredGenres;
			if (Array.isArray(list) && list.length) return list;
			return FALLBACK_GENRES;
		},

		// Полный отфильтрованный список (без учёта пагинации).
		// На реальном бэке эти же критерии станут query-параметрами.
		filteredRecommendations() {
			return this.allRecommendations.filter((b) => {
				if (
					this.filters.type !== 'all' &&
					b.recommendationType !== this.filters.type
				) return false;

				if (
					this.filters.genres.length &&
					!this.filters.genres.includes(b.genre)
				) return false;

				if (b.rating < this.filters.minRating) return false;
				if (this.filters.excludeRead && b.isRead) return false;

				return true;
			});
		},

		visibleRecommendations() {
			return this.filteredRecommendations.slice(0, this.page * PAGE_SIZE);
		},

		hasMore() {
			return this.visibleRecommendations.length < this.filteredRecommendations.length;
		}
	},

	watch: {
		// При смене любого фильтра — возвращаемся к первой «странице».
		filters: {
			deep: true,
			handler() { this.page = 1; }
		}
	},

	methods: {
		badgeColor(type) {
			return RECOMMENDATION_BADGE_COLORS[type] || 'grey';
		},

		resetFilters() {
			this.filters = { type: 'all', genres: [], minRating: 1, excludeRead: false };
			this.page = 1;
		}
	}
};
</script>

<style scoped lang="scss">
.rec-page {
	color: #fff;
	padding-top: 24px;
	padding-bottom: 60px;

	&__container {
		max-width: 1440px;
		margin: 0 auto;
		padding: 0 24px;

		@media (min-width: 1280px) {
			padding: 0 40px;
		}
	}

	&__header {
		margin-bottom: 28px;
	}

	&__title {
		font-size: 28px;
		font-weight: 800;
		letter-spacing: 0.3px;
	}

	&__subtitle {
		font-size: 14px;
		color: rgba(255, 255, 255, 0.7);
		margin-top: 4px;
	}

	&__summary {
		margin-top: 14px;
		display: flex;
		flex-wrap: wrap;
		gap: 8px;
	}

	&__summary-chip {
		font-weight: 600;
	}

	&__layout {
		display: grid;
		grid-template-columns: 260px 1fr;
		gap: 24px;
		align-items: start;

		@media (max-width: 960px) {
			grid-template-columns: 1fr;
		}
	}

	&__filters {
		position: sticky;
		top: 96px;

		@media (max-width: 960px) {
			position: static;
		}
	}

	&__results-summary {
		font-size: 13px;
		color: rgba(255, 255, 255, 0.6);
		margin-bottom: 12px;
	}
}

.filters-card {
	background: rgba(0, 0, 0, 0.7) !important;
	border-radius: 20px !important;
	border: 1px solid rgba(255, 255, 255, 0.1);
	padding: 18px;
	color: #fff;

	&__title {
		font-size: 16px;
		font-weight: 700;
		margin-bottom: 16px;
	}

	&__section {
		margin-bottom: 18px;
	}

	&__label {
		font-size: 13px;
		font-weight: 600;
		color: rgba(255, 255, 255, 0.75);
		margin-bottom: 8px;
	}

	&__radios :deep(.v-label),
	&__checkbox :deep(.v-label) {
		font-size: 13px;
		opacity: 0.95;
	}
}

.rec-card {
	position: relative;

	&__badge {
		position: absolute;
		top: 8px;
		left: 8px;
		pointer-events: none;
		font-weight: 700;
		letter-spacing: 0.3px;
		z-index: 2;
	}
}
</style>
