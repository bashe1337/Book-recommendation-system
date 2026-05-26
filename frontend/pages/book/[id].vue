<template>
	<div class="book-page">
		<!--
			Заглушка для случая, когда книги с таким id нет в моках.
			Реализована до основной разметки, чтобы шаблон не пытался
			читать поля у undefined.
		-->
		<v-container v-if="!book" class="book-page__missing">
			<v-icon size="48" color="grey">mdi-book-off-outline</v-icon>
			<h1 class="text-h5 mt-3">Книга не найдена</h1>
			<div class="text-caption text-grey">Возможно, ссылка устарела или книга удалена.</div>
			<v-btn class="mt-4" color="primary" to="/catalog">В каталог</v-btn>
		</v-container>

		<v-container v-else class="book-page__container">
			<!-- ================ Верхняя зона: обложка + метаданные ================ -->
			<section class="book-page__hero">
				<div class="book-page__cover-col">
					<!--
						Обложка. Если ссылка задана и не упала — показываем картинку.
						Иначе — SVG-заглушка BookCoverPlaceholder с названием/автором.
					-->
					<v-img
						v-if="book.cover && !coverError"
						:src="book.cover"
						:aspect-ratio="2/3"
						cover
						class="book-page__cover"
						@error="coverError = true"
					>
						<template #placeholder>
							<div class="d-flex align-center justify-center fill-height bg-grey-darken-3">
								<v-progress-circular indeterminate color="primary" size="24" />
							</div>
						</template>
					</v-img>
					<div v-else class="book-page__cover book-page__cover--placeholder">
						<BookCoverPlaceholder
							:title="book.title"
							:author="(book.authors || []).join(', ')"
							:width="220"
							:height="330"
						/>
					</div>
				</div>

				<div class="book-page__meta-col">
					<h1 class="book-page__title">{{ book.title }}</h1>

					<!--
						Авторы — кликабельные ссылки в каталог с фильтром.
						Если авторов несколько, перечисляем через запятую.
					-->
					<div class="book-page__authors">
						<template v-for="(author, idx) in book.authors" :key="author">
							<NuxtLink
								:to="`/catalog?author=${encodeURIComponent(author)}`"
								class="book-page__author-link"
							>
								{{ author }}
							</NuxtLink>
							<span v-if="idx < book.authors.length - 1">, </span>
						</template>
					</div>

					<!-- Жанры — крупные кликабельные чипы -->
					<div class="book-page__chips">
						<v-chip
							v-for="g in book.genres"
							:key="g"
							:to="`/catalog?genre=${encodeURIComponent(g)}`"
							size="small"
							class="mr-1 mb-1"
						>
							{{ g }}
						</v-chip>
					</div>

					<!-- Теги — мельче и outlined, чтобы визуально не спорить с жанрами -->
					<div class="book-page__chips book-page__chips--tags">
						<v-chip
							v-for="t in book.tags"
							:key="t"
							variant="outlined"
							size="x-small"
							class="mr-1 mb-1"
						>
							#{{ t }}
						</v-chip>
					</div>

					<!--
						Список свойств: год / язык / ISBN. ISBN — моноширинным
						шрифтом, чтобы цифры легче читались.
					-->
					<ul class="book-page__props">
						<li>
							<v-icon size="16" start>mdi-calendar</v-icon>
							{{ book.year }}
						</li>
						<li>
							<v-icon size="16" start>mdi-translate</v-icon>
							{{ book.lang }}
						</li>
						<li>
							<v-icon size="16" start>mdi-barcode</v-icon>
							<span class="book-page__isbn">{{ book.isbn }}</span>
						</li>
					</ul>

					<!--
						Описание. Полностью раскрывается по клику.
						Класс --collapsed применяет line-clamp: 4 — это и есть
						«4 строки» по требованию ТЗ. Кнопку показываем всегда,
						чтобы пользователь мог свернуть длинный текст обратно.
					-->
					<p
						class="book-page__description"
						:class="{ 'book-page__description--collapsed': !descriptionExpanded }"
					>
						{{ book.description }}
					</p>
					<v-btn
						variant="text"
						size="small"
						class="book-page__description-toggle"
						@click="descriptionExpanded = !descriptionExpanded"
					>
						{{ descriptionExpanded ? 'Свернуть' : 'Развернуть' }}
						<v-icon end>
							{{ descriptionExpanded ? 'mdi-chevron-up' : 'mdi-chevron-down' }}
						</v-icon>
					</v-btn>

					<!--
						Действия. Доступны только авторизованному пользователю.
						Для неавторизованного — disabled + тултип.
					-->
					<div class="book-page__actions">
						<v-tooltip :disabled="isAuthenticated" location="top" text="Войдите, чтобы добавить">
							<template #activator="{ props }">
								<div v-bind="props" class="book-page__action-wrapper">
									<v-btn
										variant="outlined"
										:prepend-icon="isFavorite ? 'mdi-heart' : 'mdi-heart-outline'"
										:color="isFavorite ? 'pink' : undefined"
										:disabled="!isAuthenticated"
										@click="toggleFavorite"
									>
										{{ isFavorite ? 'В избранном' : 'В избранное' }}
									</v-btn>
								</div>
							</template>
						</v-tooltip>

						<!-- <v-tooltip :disabled="isAuthenticated" location="top" text="Войдите, чтобы добавить">
							<template #activator="{ props }">
								<div v-bind="props" class="book-page__action-wrapper">
									<v-btn
										variant="outlined"
										:prepend-icon="isRead ? 'mdi-check-circle' : 'mdi-check-circle-outline'"
										:color="isRead ? 'success' : undefined"
										:disabled="!isAuthenticated"
										@click="toggleRead"
									>
										{{ isRead ? 'Прочитано' : 'Отметить прочитанной' }}
									</v-btn>
								</div>
							</template>
						</v-tooltip> -->
					</div>

					<!--
						Агрегированный рейтинг. Помещаем под действиями, как и в макете.
						Большое число + звёзды + текст «на основе N оценок» +
						гистограмма распределения.
					-->
					<div class="book-page__rating-block">
						<div class="book-page__rating-top">
							<div class="book-page__rating-value">{{ mockRating.average.toFixed(1) }}</div>
							<div>
								<v-rating
									:model-value="mockRating.average"
									readonly
									half-increments
									density="compact"
									color="amber"
									size="small"
								/>
								<div class="book-page__rating-count">
									на основе {{ mockRating.count.toLocaleString('ru-RU') }} оценок
								</div>
							</div>
						</div>

						<!--
							Распределение от 5 к 1, как в большинстве магазинов.
							Полоска (v-progress-linear) + процент справа.
						-->
						<div class="book-page__rating-bars">
							<div
								v-for="star in [5, 4, 3, 2, 1]"
								:key="star"
								class="book-page__rating-bar-row"
							>
								<span class="book-page__rating-bar-label">
									{{ star }}<v-icon size="12" color="amber">mdi-star</v-icon>
								</span>
								<v-progress-linear
									:model-value="mockRating.distribution[star]"
									height="6"
									rounded
									color="amber"
									bg-color="grey-darken-3"
									class="book-page__rating-bar"
								/>
								<span class="book-page__rating-bar-percent">
									{{ mockRating.distribution[star] }}%
								</span>
							</div>
						</div>
					</div>
				</div>
			</section>

			<!-- ================ Нижняя зона: отзывы + сайдбар ================ -->
			<section class="book-page__body">
				<!-- ===== Основная колонка: форма + отзывы ===== -->
				<div class="book-page__reviews-col">
					<!--
						Форма отзыва — только для авторизованных.
						Иначе — блок-заглушка с CTA на /login.
					-->
					<div class="book-page__review-form" v-if="isAuthenticated">
						<h2 class="book-page__section-title">Ваш отзыв</h2>

						<div class="book-page__form-row">
							<span class="book-page__form-label">Оценка:</span>
							<v-rating
								v-model="form.rating"
								color="amber"
								active-color="amber"
								hover
								density="compact"
							/>
						</div>

						<v-textarea
							v-model="form.text"
							placeholder="Поделитесь впечатлениями (минимум 10 символов)"
							variant="outlined"
							rows="4"
							:counter="MAX_REVIEW_LENGTH"
							:maxlength="MAX_REVIEW_LENGTH"
							:rules="reviewTextRules"
						/>

						<div class="book-page__form-actions">
							<v-btn
								color="primary"
								:disabled="!canSubmitReview"
								@click="submitReview"
							>
								Опубликовать
							</v-btn>
						</div>
					</div>

					<div v-else class="book-page__login-cta">
						<v-icon size="32" color="grey">mdi-account-lock-outline</v-icon>
						<div class="mt-2">Войдите, чтобы оставить отзыв</div>
						<v-btn class="mt-3" color="primary" to="/login">Войти</v-btn>
					</div>

					<!-- ===== Заголовок и фильтры тональности ===== -->
					<div class="book-page__reviews-header">
						<h2 class="book-page__section-title">Отзывы ({{ filteredReviews.length }})</h2>

						<!-- <v-btn-toggle
							v-model="sentimentFilter"
							mandatory
							density="compact"
							color="primary"
							class="book-page__sentiment-toggle"
						>
							<v-btn value="all" size="small">Все</v-btn>
							<v-btn value="positive" size="small">Позитивные</v-btn>
							<v-btn value="neutral" size="small">Нейтральные</v-btn>
							<v-btn value="negative" size="small">Негативные</v-btn>
						</v-btn-toggle> -->
					</div>

					<!-- ===== Список отзывов с локальной пагинацией ===== -->
					<div class="book-page__reviews-list">
						<div
							v-for="review in pagedReviews"
							:key="review.id"
							class="review-card"
						>
							<div class="review-card__head">
								<v-avatar size="36" color="primary" class="review-card__avatar">
									<span class="review-card__initials">{{ getInitials(review.user) }}</span>
								</v-avatar>
								<div class="review-card__meta">
									<div class="review-card__user">{{ review.user }}</div>
									<div class="review-card__date">{{ formatDate(review.date) }}</div>
								</div>
								<v-spacer />
								<!-- <v-chip
									:color="sentimentColor(review.sentiment)"
									size="x-small"
									variant="tonal"
									class="review-card__sentiment"
								>
									{{ sentimentLabel(review.sentiment) }}
								</v-chip> -->
							</div>

							<v-rating
								:model-value="review.rating"
								readonly
								density="compact"
								color="amber"
								size="small"
								class="review-card__rating"
							/>

							<p class="review-card__text">{{ review.text }}</p>

							<div class="review-card__footer">
								<v-btn
									:variant="review.likedByMe ? 'flat' : 'tonal'"
									:color="review.likedByMe ? 'primary' : undefined"
									size="small"
									prepend-icon="mdi-thumb-up-outline"
									@click="toggleLike(review)"
								>
									Полезно · {{ review.likes }}
								</v-btn>
							</div>
						</div>

						<div
							v-if="!filteredReviews.length"
							class="text-center text-caption text-grey py-4"
						>
							Пока нет отзывов с такой тональностью
						</div>
					</div>

					<div v-if="hasMoreReviews" class="text-center mt-4">
						<v-btn variant="tonal" @click="reviewsPage++">
							Загрузить ещё
						</v-btn>
					</div>
				</div>

				<!-- ===== Боковая колонка: похожие книги ===== -->
				<aside class="book-page__sidebar">
					<h2 class="book-page__section-title">С этим также читают</h2>
					<div class="book-page__similar-list">
						<BookCardCompact
							v-for="similar in mockSimilarBooks"
							:key="similar.id"
							:book="similar"
						/>
					</div>
					<v-btn
						block
						variant="text"
						class="mt-2"
						:to="`/catalog?similar=${book.id}`"
					>
						Показать больше
						<v-icon end>mdi-chevron-right</v-icon>
					</v-btn>
				</aside>
			</section>
		</v-container>

		<v-snackbar v-model="snackbar.show" :timeout="2500" color="success">
			{{ snackbar.text }}
		</v-snackbar>
	</div>
</template>

<script>
import { storeToRefs } from 'pinia';
import { useUserStore } from '~/stores/user';

import BookCardCompact from '~/components/common/BookCardCompact.vue';
import BookCoverPlaceholder from '~/components/common/BookCoverPlaceholder.vue';

import { mapBook, mapBookSummary, mapBookList } from '~/utils/bookAdapters.js';

const MAX_REVIEW_LENGTH = 5000;   // совпадает с CreateReviewRequest на бэке
const MIN_REVIEW_LENGTH = 10;
const REVIEWS_PER_PAGE  = 5;

// Бэкенд (BookStatsDto) пока не отдаёт распределение оценок по звёздам —
// синтезируем его из средней оценки для гистограммы. Когда бэк начнёт
// возвращать distribution — просто заменим эту функцию.
const synthDistribution = (avg) => {
	if (!avg) return { 5: 0, 4: 0, 3: 0, 2: 0, 1: 0 };
	const a = Math.max(1, Math.min(5, avg));
	// «Колокол» вокруг средней оценки.
	const weight = (s) => Math.max(0, 1 - Math.abs(s - a) * 0.55);
	const raw = { 5: weight(5), 4: weight(4), 3: weight(3), 2: weight(2), 1: weight(1) };
	const total = raw[5] + raw[4] + raw[3] + raw[2] + raw[1] || 1;
	return {
		5: Math.round((raw[5] / total) * 100),
		4: Math.round((raw[4] / total) * 100),
		3: Math.round((raw[3] / total) * 100),
		2: Math.round((raw[2] / total) * 100),
		1: Math.round((raw[1] / total) * 100)
	};
};

// ReviewDto: { id, bookId, userId, username, content, createdAt, sentimentScore }
// На бэке SentimentScore вычисляется ML асинхронно (float | null).
// Считаем: >=0.6 positive, 0.4..0.6 neutral, <0.4 negative; null → neutral.
const deriveSentiment = (score) => {
	if (score === null || score === undefined) return 'neutral';
	if (score >= 0.6) return 'positive';
	if (score <  0.4) return 'negative';
	return 'neutral';
};

const mapReview = (dto) => ({
	id:        dto.id,
	user:      dto.username || 'Аноним',
	avatar:    null,
	date:      dto.createdAt,
	text:      dto.content || '',
	// У ReviewDto на бэке нет «score» — рейтинг живёт в отдельной сущности Rating.
	// Подгружать оценку каждого автора слишком дорого, оставляем 0 — звёзды
	// в карточке отзыва не показываем.
	rating:    0,
	sentiment: deriveSentiment(dto.sentimentScore)
});

export default {
	name: 'BookPage',

	components: { BookCardCompact, BookCoverPlaceholder },

	setup() {
		const route = useRoute();
		const userStore = useUserStore();
		const { isAuthenticated } = storeToRefs(userStore);
		const api = useApi();

		// Маршрут /book/:id — id это Guid (строка) на бэке.
		const bookId = String(route.params.id);

		return { route, api, isAuthenticated, bookId };
	},

	data() {
		return {
			MAX_REVIEW_LENGTH,
			loading: true,

			// Книга и её статистика подгружаются параллельно.
			book: null,
			mockRating: { average: 0, count: 0, distribution: { 5: 0, 4: 0, 3: 0, 2: 0, 1: 0 } },
			mockSimilarBooks: [],

			coverError: false,
			descriptionExpanded: false,

			isFavorite: false,
			// Кнопка «Прочитано» теперь фиксирует view (POST /api/books/{id}/view).
			isRead: false,

			// Форма отзыва: оценка + текст. Отправляются двумя запросами
			// (POST /api/books/{id}/ratings и POST /api/books/{id}/reviews).
			form: { rating: 0, text: '' },

			// Список отзывов с бэка.
			sentimentFilter: 'all',
			reviews: [],
			reviewsPage: 1,
			reviewsTotal: 0,

			snackbar: { show: false, text: '' }
		};
	},

	computed: {
		reviewTextRules() {
			return [
				(v) =>
					!v || v.length >= MIN_REVIEW_LENGTH ||
					`Минимум ${MIN_REVIEW_LENGTH} символов`
			];
		},

		canSubmitReview() {
			return (
				this.form.rating > 0 &&
				this.form.text.trim().length >= MIN_REVIEW_LENGTH &&
				this.form.text.length <= MAX_REVIEW_LENGTH
			);
		},

		filteredReviews() {
			if (this.sentimentFilter === 'all') return this.reviews;
			return this.reviews.filter((r) => r.sentiment === this.sentimentFilter);
		},

		pagedReviews() {
			return this.filteredReviews.slice(0, this.reviewsPage * REVIEWS_PER_PAGE);
		},

		hasMoreReviews() {
			return this.pagedReviews.length < this.filteredReviews.length;
		}
	},

	watch: {
		sentimentFilter() {
			this.reviewsPage = 1;
		}
	},

	async mounted() {
		await this.loadBook();
		if (!this.book) return;

		// Дальше — параллельно подгружаем статистику, отзывы, похожих,
		// статус избранного и факт авто-просмотра (для history).
		await Promise.allSettled([
			this.loadStats(),
			this.loadReviews(),
			this.loadSimilar(),
			this.loadFavoriteStatus(),
			this.recordView()
		]);
	},

	methods: {
		// GET /api/books/{id} → BookDto
		async loadBook() {
			this.loading = true;
			try {
				const dto = await this.api.get(`/api/books/${this.bookId}`);
				this.book = mapBook(dto);
				this.mockRating.average = this.book.rating || 0;
				this.mockRating.count = this.book.ratingCount || 0;
				this.mockRating.distribution = synthDistribution(this.book.rating);
			} catch {
				this.book = null;
			} finally {
				this.loading = false;
			}
		},

		// GET /api/books/{id}/stats — точные числа avg/count + reviewCount/favoriteCount.
		async loadStats() {
			try {
				const stats = await this.api.get(`/api/books/${this.bookId}/stats`);
				this.mockRating.average = stats?.avgRating ?? this.mockRating.average;
				this.mockRating.count = stats?.ratingCount ?? this.mockRating.count;
				this.mockRating.distribution = synthDistribution(this.mockRating.average);
			} catch { /* оставляем то, что пришло с книгой */ }
		},

		// GET /api/books/{id}/reviews → PagedResult<ReviewDto>
		async loadReviews() {
			try {
				const res = await this.api.get(`/api/books/${this.bookId}/reviews`, {
					query: { page: 1, pageSize: 50 }
				});
				const list = Array.isArray(res?.items) ? res.items : [];
				this.reviews = list.map(mapReview);
				this.reviewsTotal = res?.totalCount ?? this.reviews.length;
			} catch {
				this.reviews = [];
			}
		},

		// GET /api/books/{id}/similar → BookSummaryDto[] (бэк уже сортирует по жанрам).
		async loadSimilar() {
			try {
				const list = await this.api.get(`/api/books/${this.bookId}/similar`, {
					query: { limit: 6 }
				});
				this.mockSimilarBooks = mapBookList(list);
			} catch {
				this.mockSimilarBooks = [];
			}
		},

		// GET /api/users/me/favorites/{id}/check → { isFavorited }
		async loadFavoriteStatus() {
			if (!this.isAuthenticated) return;
			try {
				const res = await this.api.get(`/api/users/me/favorites/${this.bookId}/check`);
				this.isFavorite = !!res?.isFavorited;
			} catch { this.isFavorite = false; }
		},

		// POST /api/books/{id}/view — фиксируем просмотр в истории.
		// Дедупликация на бэке: один просмотр в сутки.
		async recordView() {
			if (!this.isAuthenticated) return;
			try {
				await this.api.post(`/api/books/${this.bookId}/view`);
				this.isRead = true;
			} catch { /* ничего критичного, страница уже отображена */ }
		},

		// POST/DELETE /api/users/me/favorites/{id}
		async toggleFavorite() {
			if (!this.isAuthenticated) return;
			const next = !this.isFavorite;
			// Оптимистичное обновление — откатываемся при ошибке.
			this.isFavorite = next;
			try {
				if (next) await this.api.post(`/api/users/me/favorites/${this.bookId}`);
				else      await this.api.del(`/api/users/me/favorites/${this.bookId}`);
			} catch {
				this.isFavorite = !next;
			}
		},

		// Перезаписываем «отметить просмотренной» как явный POST /view.
		async toggleRead() {
			if (!this.isAuthenticated) return;
			try {
				await this.api.post(`/api/books/${this.bookId}/view`);
				this.isRead = true;
			} catch { /* noop */ }
		},

		// Отправка отзыва = две операции:
		//   1) POST /api/books/{id}/ratings { score }
		//   2) POST /api/books/{id}/reviews { content }
		async submitReview() {
			if (!this.canSubmitReview || !this.isAuthenticated) return;

			try {
				await this.api.post(`/api/books/${this.bookId}/ratings`, {
					score: this.form.rating
				});
			} catch { /* если оценка не прошла — отзыв всё равно попробуем */ }

			try {
				const dto = await this.api.post(
					`/api/books/${this.bookId}/reviews`,
					{ content: this.form.text.trim() }
				);
				// Подкладываем новый отзыв в начало списка локально —
				// без перезагрузки, чтобы UI был отзывчивым.
				this.reviews = [mapReview(dto), ...this.reviews];
				this.form = { rating: 0, text: '' };
				this.sentimentFilter = 'all';
				this.reviewsPage = 1;
				this.snackbar = { show: true, text: 'Отзыв опубликован' };
				// Обновляем статистику книги — там изменились и avg, и reviewCount.
				this.loadStats();
			} catch (err) {
				this.snackbar = {
					show: true,
					text: err?.data?.message || 'Не удалось опубликовать отзыв'
				};
			}
		},

		// На бэке нет like-эндпоинта — оставляем UI-only no-op.
		toggleLike(review) {
			review.likedByMe = !review.likedByMe;
			review.likes = (review.likes || 0) + (review.likedByMe ? 1 : -1);
		},

		getInitials(name) {
			if (!name) return '?';
			const parts = name.trim().split(/\s+/);
			return (parts[0][0] + (parts[1]?.[0] || '')).toUpperCase();
		},

		formatDate(iso) {
			if (!iso) return '';
			const d = new Date(iso);
			return d.toLocaleDateString('ru-RU', {
				day: '2-digit', month: 'long', year: 'numeric'
			});
		},

		sentimentLabel(s) {
			return { positive: 'позитивный', neutral: 'нейтральный', negative: 'негативный' }[s] || s;
		},
		sentimentColor(s) {
			return { positive: 'success', neutral: 'grey', negative: 'error' }[s] || 'grey';
		}
	}
};
</script>

<style scoped lang="scss">
.book-page {
	color: #fff;
	padding-top: 24px;
	padding-bottom: 60px;

	&__container {
		max-width: 1280px;
		margin: 0 auto;
		padding: 0 24px;
	}

	&__missing {
		max-width: 480px;
		margin: 80px auto;
		text-align: center;
	}

	// ============ HERO ============
	&__hero {
		display: grid;
		grid-template-columns: 220px 1fr;
		gap: 32px;
		margin-bottom: 48px;

		@media (max-width: 960px) {
			grid-template-columns: 1fr;
			justify-items: center;
			text-align: center;
		}
	}

	&__cover-col {
		width: 220px;

		@media (max-width: 960px) {
			width: 180px;
		}
	}

	&__cover {
		border-radius: 12px;
		overflow: hidden;
		box-shadow: 0 20px 50px rgba(0, 0, 0, 0.5);
		background: #2a2a2a;
	}

	&__cover--placeholder {
		// SVG-заглушка сама держит соотношение через viewBox;
		// тут только убираем фон, чтобы не пробивались дефолтные стили v-img.
		background: transparent;
		aspect-ratio: 2 / 3;
		display: block;
	}

	&__title {
		font-size: 32px;
		font-weight: 800;
		line-height: 1.15;
		letter-spacing: 0.3px;

		@media (max-width: 600px) {
			font-size: 24px;
		}
	}

	&__authors {
		margin-top: 8px;
		font-size: 15px;
		color: rgba(255, 255, 255, 0.75);
	}

	&__author-link {
		color: #fff;
		text-decoration: none;
		border-bottom: 1px dashed rgba(255, 255, 255, 0.3);

		&:hover {
			border-bottom-color: #fff;
		}
	}

	&__chips {
		margin-top: 12px;

		&--tags {
			margin-top: 4px;
		}
	}

	&__props {
		list-style: none;
		padding: 0;
		margin: 16px 0;
		display: flex;
		flex-wrap: wrap;
		gap: 16px;
		color: rgba(255, 255, 255, 0.75);
		font-size: 14px;

		li {
			display: flex;
			align-items: center;
		}

		@media (max-width: 960px) {
			justify-content: center;
		}
	}

	&__isbn {
		font-family: 'Roboto Mono', monospace;
		letter-spacing: 0.5px;
	}

	&__description {
		font-size: 15px;
		line-height: 1.6;
		color: rgba(255, 255, 255, 0.85);
		margin: 12px 0 4px;
		white-space: pre-line;

		&--collapsed {
			display: -webkit-box;
			-webkit-line-clamp: 4;
			-webkit-box-orient: vertical;
			overflow: hidden;
		}
	}

	&__description-toggle {
		text-transform: none;
		padding: 0 8px !important;
	}

	&__actions {
		display: flex;
		flex-wrap: wrap;
		gap: 12px;
		margin: 20px 0 28px;

		@media (max-width: 960px) {
			justify-content: center;
		}
	}

	// нужна обёртка вокруг disabled-кнопки, чтобы тултип ловил hover
	&__action-wrapper {
		display: inline-flex;
	}

	// ============ RATING BLOCK ============
	&__rating-block {
		background: rgba(255, 255, 255, 0.04);
		border: 1px solid rgba(255, 255, 255, 0.08);
		border-radius: 16px;
		padding: 16px;
		max-width: 460px;

		@media (max-width: 960px) {
			margin: 0 auto;
		}
	}

	&__rating-top {
		display: flex;
		align-items: center;
		gap: 16px;
		margin-bottom: 12px;
	}

	&__rating-value {
		font-size: 40px;
		font-weight: 800;
		line-height: 1;
	}

	&__rating-count {
		font-size: 12px;
		color: rgba(255, 255, 255, 0.6);
	}

	&__rating-bars {
		display: flex;
		flex-direction: column;
		gap: 6px;
	}

	&__rating-bar-row {
		display: grid;
		grid-template-columns: 36px 1fr 40px;
		gap: 8px;
		align-items: center;
		font-size: 12px;
		color: rgba(255, 255, 255, 0.75);
	}

	&__rating-bar-label {
		display: inline-flex;
		align-items: center;
		gap: 2px;
	}

	&__rating-bar-percent {
		text-align: right;
	}

	// ============ BODY ============
	&__body {
		display: grid;
		grid-template-columns: minmax(0, 65%) minmax(0, 35%);
		gap: 32px;
		align-items: start;

		@media (max-width: 960px) {
			grid-template-columns: 1fr;
			gap: 24px;
		}
	}

	&__section-title {
		font-size: 20px;
		font-weight: 700;
		letter-spacing: 0.3px;
		margin-bottom: 16px;
	}

	&__review-form {
		background: rgba(255, 255, 255, 0.04);
		border: 1px solid rgba(255, 255, 255, 0.08);
		border-radius: 16px;
		padding: 20px;
		margin-bottom: 28px;
	}

	&__form-row {
		display: flex;
		align-items: center;
		gap: 12px;
		margin-bottom: 12px;
	}

	&__form-label {
		font-size: 14px;
		color: rgba(255, 255, 255, 0.7);
	}

	&__form-actions {
		display: flex;
		justify-content: flex-end;
	}

	&__login-cta {
		text-align: center;
		padding: 24px;
		background: rgba(255, 255, 255, 0.04);
		border: 1px dashed rgba(255, 255, 255, 0.15);
		border-radius: 16px;
		margin-bottom: 28px;
		color: rgba(255, 255, 255, 0.75);
	}

	&__reviews-header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		flex-wrap: wrap;
		gap: 12px;
		margin-bottom: 16px;
	}

	&__sentiment-toggle {
		flex-wrap: wrap;
	}

	&__reviews-list {
		display: flex;
		flex-direction: column;
		gap: 12px;
	}

	&__sidebar {
		// На десктопе залипает рядом со скроллящимися отзывами.
		position: sticky;
		top: 96px;

		@media (max-width: 960px) {
			position: static;
		}
	}

	&__similar-list {
		display: flex;
		flex-direction: column;
		gap: 4px;
		background: rgba(255, 255, 255, 0.03);
		border: 1px solid rgba(255, 255, 255, 0.06);
		border-radius: 16px;
		padding: 8px;
	}
}

// ============ REVIEW CARD ============
.review-card {
	background: rgba(255, 255, 255, 0.04);
	border: 1px solid rgba(255, 255, 255, 0.06);
	border-radius: 14px;
	padding: 16px;

	&__head {
		display: flex;
		align-items: center;
		gap: 12px;
		margin-bottom: 8px;
	}

	&__initials {
		font-size: 13px;
		font-weight: 700;
		color: #fff;
	}

	&__user {
		font-size: 14px;
		font-weight: 600;
	}

	&__date {
		font-size: 12px;
		color: rgba(255, 255, 255, 0.5);
	}

	&__rating {
		margin: 4px 0 8px;
	}

	&__text {
		font-size: 14px;
		line-height: 1.55;
		color: rgba(255, 255, 255, 0.85);
		margin-bottom: 12px;
		white-space: pre-line;
	}

	&__footer {
		display: flex;
		justify-content: flex-end;
	}
}
</style>
