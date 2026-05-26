<template>
	<div class="rec-page">
		<v-container class="rec-page__container">
			<!--
				ШАПКА — заголовок + персонализированный подзаголовок
				+ цветные счётчики-чипы. Цифры берутся из реальных эндпоинтов:
				stats-плитки заполняются после загрузки.
			-->
			<header class="rec-page__header">
				<h1 class="rec-page__title">Рекомендации для&nbsp;вас</h1>
				<p class="rec-page__subtitle">
					Подобрано специально для <b>{{ currentUserName }}</b>
					на основе ваших оценок и&nbsp;истории чтения
				</p>

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

			<!-- БЛОК 1 — «Рекомендовано вам» (бэк: SVD) -->
			<BookCarousel
				v-if="forYou.length"
				title="Рекомендовано вам"
				:caption="summary.ratingsCount > 10 ? `Подобрано лично для вас на основе ваших оценок и истории чтения` : `Подобрано на основе ваших жанров и предпочтений`"
				:books="forYou"
				view-all-to="/catalog?filter=for-you"
			/>

			<!-- БЛОК 2 — «Вам может понравиться» (бэк: ALS) -->
			<BookCarousel
				v-if="mayLike.length && summary.ratingsCount > 10"
				title="Вам может понравиться"
				caption="Книги, которые мы предлагаем попробовать — выходят за привычные жанры, но близки по духу"
				:books="mayLike"
				view-all-to="/catalog?filter=may-like"
			/>

			<!--
				БЛОК 3 — «Похожее на «X»» для до 10 оценённых книг.
				Бэк: GET /api/recommendations/based-on-my-ratings
				   → { groups: [{ source, model, items }, ...] }
			-->
			<section
				v-for="group in similarGroups"
				:key="group.source.id"
				class="rec-page__similar-group"
			>
				<div class="rec-group__header">
					<div class="rec-group__heading">
						<h2 class="rec-group__title">
							Похожее на «{{ group.source.title }}»
						</h2>
						<div class="rec-group__caption">
							{{ group.caption }}
						</div>
					</div>
					<NuxtLink
						v-if="group.source.id"
						:to="`/book/${group.source.id}`"
						class="rec-group__source-link"
					>
						перейти к оценённой книге
						<v-icon size="14" end>mdi-arrow-right</v-icon>
					</NuxtLink>
				</div>

				<v-row dense>
					<v-col
						v-for="book in group.items"
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

			<EmptyState
				v-if="!loading && !forYou.length && !mayLike.length && !similarGroups.length"
				icon="mdi-book-search-outline"
				text="Оцените несколько книг — и здесь появятся рекомендации"
				action-text="Перейти в каталог"
				action-to="/catalog"
			/>

			<div v-if="loading" class="text-center py-8">
				<v-progress-circular indeterminate color="primary" size="36" />
			</div>
		</v-container>
	</div>
</template>

<script>
import { storeToRefs } from 'pinia';
import { useUserStore } from '~/stores/user';

import BookCard from '~/components/common/BookCard.vue';
import BookCarousel from '~/components/common/BookCarousel.vue';
import EmptyState from '~/components/common/EmptyState.vue';
import { mapBookSummary, mapBookList } from '~/utils/bookAdapters.js';

definePageMeta({ middleware: 'auth' });

// Подписи под группой «Похожее на X». Чередуются, чтобы блоки звучали по-разному.
const SIMILAR_CAPTIONS = [
	'Книги, которые часто читают вместе с этой',
	'Похожие по сюжету, темам и атмосфере'
];

export default {
	name: 'RecommendationsPage',

	components: { BookCard, BookCarousel, EmptyState },

	setup() {
		const userStore = useUserStore();
		const { user } = storeToRefs(userStore);
		const api = useApi();
		return { userStore, user, api };
	},

	data() {
		return {
			loading: true,
			forYou: [],
			mayLike: [],
			similarGroups: [],

			// Сводка цифр для шапки. Заполняем из реальных счётчиков —
			// favorites/history/ratings (см. mounted).
			summary: {
				ratingsCount: 0,
				favoriteGenresCount: 0,
				viewedBooksCount: 0
			}
		};
	},

	computed: {
		currentUserName() {
			return this.user?.username || 'вас';
		}
	},

	async mounted() {
		this.summary.favoriteGenresCount = this.user?.preferredGenreIds?.length || 0;

		await Promise.allSettled([
			this.loadForYou(),
			this.loadMayLike(),
			this.loadBasedOnMyRatings(),
			this.loadSummary()
		]);

		this.loading = false;
	},

	methods: {
		// GET /api/recommendations/for-you → RecommendationListDto
		async loadForYou() {
			try {
				const res = await this.api.get('/api/recommendations/for-you', { query: { limit: 12 } });
				this.forYou = mapBookList(res?.items);
			} catch {
				this.forYou = [];
			}
		},

		// GET /api/recommendations/may-like → RecommendationListDto
		async loadMayLike() {
			try {
				const res = await this.api.get('/api/recommendations/may-like', { query: { limit: 12 } });
				this.mayLike = mapBookList(res?.items);
			} catch {
				this.mayLike = [];
			}
		},

		// GET /api/recommendations/based-on-my-ratings → BasedOnMyRatingsResponseDto
		// { groups: [{ source: BookSummary, model, items: BookSummary[] }] }
		async loadBasedOnMyRatings() {
			try {
				const res = await this.api.get('/api/recommendations/based-on-my-ratings', {
					query: { sources: 10, perSource: 8 }
				});
				const groups = Array.isArray(res?.groups) ? res.groups : [];
				this.similarGroups = groups
					.map((g, idx) => ({
						source: mapBookSummary(g.source),
						items: mapBookList(g.items),
						caption: SIMILAR_CAPTIONS[idx % SIMILAR_CAPTIONS.length]
					}))
					.filter((g) => g.source && g.items.length > 0);
			} catch {
				this.similarGroups = [];
			}
		},

		// Цифры для счётчиков шапки. Берём первую страницу истории/избранного/отзывов
		// — totalCount даёт нужные значения без отдельного эндпоинта статистики.
		async loadSummary() {
			const tasks = [
				this.api.get('/api/users/me/history', { query: { page: 1, pageSize: 1 } })
					.then((r) => { this.summary.viewedBooksCount = r?.totalCount ?? 0; })
					.catch(() => {}),
				// Простая прокси-метрика: возьмём количество отзывов как «оценок».
				// На реальном бэке есть отдельный счётчик оценок — добавим, когда появится.
				this.api.get('/api/users/me/reviews', { query: { page: 1, pageSize: 1 } })
					.then((r) => { this.summary.ratingsCount = r?.totalCount ?? 0; })
					.catch(() => {})
			];
			await Promise.allSettled(tasks);
		}
	}
};
</script>

<style scoped lang="scss">
.rec-page {
	color: #fff;
	padding-top: 24px;
	padding-bottom: 80px;

	&__container {
		max-width: 1440px;
		margin: 0 auto;
		padding: 0 24px;

		@media (min-width: 1280px) { padding: 0 40px; }
	}

	&__header {
		margin-bottom: 40px;
	}

	&__title {
		font-size: 38px;
		font-weight: 800;
		letter-spacing: 0.2px;
		line-height: 1.1;

		@media (max-width: 600px) { font-size: 28px; }
	}

	&__subtitle {
		font-size: 15px;
		color: rgba(255, 255, 255, 0.7);
		margin-top: 8px;
		max-width: 720px;
	}

	&__summary {
		margin-top: 18px;
		display: flex;
		flex-wrap: wrap;
		gap: 8px;
	}

	&__summary-chip {
		font-weight: 600;
	}

	&__similar-group {
		margin-bottom: 56px;
	}
}

.rec-group {
	&__header {
		display: flex;
		align-items: flex-start;
		justify-content: space-between;
		gap: 16px;
		flex-wrap: wrap;
		margin-bottom: 22px;
		padding-bottom: 12px;
		border-bottom: 1px solid rgba(255, 255, 255, 0.08);
	}

	&__heading {
		min-width: 0;
	}

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

	&__source-link {
		margin-left: auto;
		font-size: 13px;
		color: rgba(255, 255, 255, 0.6);
		text-decoration: none;
		display: inline-flex;
		align-items: center;
		gap: 4px;
		flex-shrink: 0;

		&:hover { color: #fff; }
	}
}
</style>
