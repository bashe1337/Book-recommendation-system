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

						<v-tooltip :disabled="isAuthenticated" location="top" text="Войдите, чтобы добавить">
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
						</v-tooltip>
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

						<v-btn-toggle
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
						</v-btn-toggle>
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
								<v-chip
									:color="sentimentColor(review.sentiment)"
									size="x-small"
									variant="tonal"
									class="review-card__sentiment"
								>
									{{ sentimentLabel(review.sentiment) }}
								</v-chip>
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

// === MOCK DATA — убрать, когда бэкенд будет готов ===
import {
	mockBooks,
	mockRating,
	mockReviews,
	mockSimilarBooks
} from '~/mocks/bookMocks.js';

const MAX_REVIEW_LENGTH = 1000;
const MIN_REVIEW_LENGTH = 10;
const REVIEWS_PER_PAGE  = 5;

export default {
	name: 'BookPage',

	components: { BookCardCompact, BookCoverPlaceholder },

	setup() {
		const route = useRoute();
		const userStore = useUserStore();
		const { isAuthenticated } = storeToRefs(userStore);

		// id из URL может быть строкой; в моках ключи — числа. Приводим.
		const bookId = Number(route.params.id);
		const book = mockBooks[bookId] || null;

		return {
			isAuthenticated,
			book,
			mockRating,
			mockSimilarBooks
		};
	},

	data() {
		return {
			MAX_REVIEW_LENGTH,

			// Флаг ошибки загрузки обложки — переключает v-img на SVG-заглушку.
			coverError: false,

			descriptionExpanded: false,

			// локальные «состояния» действий — пока живут на клиенте, без бэкенда
			isFavorite: false,
			isRead: false,

			// форма отзыва
			form: { rating: 0, text: '' },

			// фильтр тональности и список отзывов с локальной копией
			// (нужна, чтобы добавить новый отзыв в начало, не мутируя моки)
			sentimentFilter: 'all',
			reviews: [...mockReviews],

			// пагинация: показываем по 5, увеличиваем reviewsPage
			reviewsPage: 1,

			snackbar: { show: false, text: '' }
		};
	},

	computed: {
		// Валидация полей формы отзыва.
		// Используется и для дизейбла кнопки, и для rules в v-textarea.
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

		// Фильтрация отзывов по тональности.
		filteredReviews() {
			if (this.sentimentFilter === 'all') return this.reviews;
			return this.reviews.filter((r) => r.sentiment === this.sentimentFilter);
		},

		// «Окно» с учётом текущей страницы.
		pagedReviews() {
			return this.filteredReviews.slice(0, this.reviewsPage * REVIEWS_PER_PAGE);
		},

		hasMoreReviews() {
			return this.pagedReviews.length < this.filteredReviews.length;
		}
	},

	watch: {
		// При смене фильтра — возвращаемся к первой странице,
		// иначе можно увидеть пустоту, если в фильтре <5 элементов.
		sentimentFilter() {
			this.reviewsPage = 1;
		}
	},

	methods: {
		// TODO: POST /api/favorites { bookId }
		toggleFavorite() {
			this.isFavorite = !this.isFavorite;
		},

		// TODO: POST /api/reading-history { bookId }
		toggleRead() {
			this.isRead = !this.isRead;
		},

		// TODO: POST /api/reviews { bookId, rating, text }
		// На бэке также произойдёт sentiment analysis — тональность придёт оттуда.
		// Сейчас имитируем: если оценка >=4 — positive, ==3 — neutral, иначе negative.
		submitReview() {
			if (!this.canSubmitReview) return;

			const sentiment =
				this.form.rating >= 4 ? 'positive' :
				this.form.rating === 3 ? 'neutral' : 'negative';

			this.reviews.unshift({
				id: Date.now(),
				user: 'Вы',
				avatar: null,
				date: new Date().toISOString().slice(0, 10),
				rating: this.form.rating,
				sentiment,
				likes: 0,
				text: this.form.text.trim()
			});

			this.form = { rating: 0, text: '' };
			this.sentimentFilter = 'all';
			this.reviewsPage = 1;
			this.snackbar = { show: true, text: 'Отзыв опубликован' };
		},

		// «Полезно»: на моках просто инкрементим/декрементим локально.
		// TODO: POST /api/reviews/:id/like — при подключении бэкенда.
		toggleLike(review) {
			review.likedByMe = !review.likedByMe;
			review.likes += review.likedByMe ? 1 : -1;
		},

		getInitials(name) {
			if (!name) return '?';
			const parts = name.trim().split(/\s+/);
			return (parts[0][0] + (parts[1]?.[0] || '')).toUpperCase();
		},

		formatDate(iso) {
			// Простое локальное форматирование, чтобы не тащить date-fns.
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
