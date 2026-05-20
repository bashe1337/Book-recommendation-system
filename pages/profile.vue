<template>
	<div class="profile-page">
		<v-container class="profile-page__container">
			<!--
				========================================================
				ВЕРХНЯЯ ЗОНА — карточка пользователя.
				Аватар + имя + метаданные + три метрики в ряд.
				========================================================
			-->
			<v-card class="profile-card" elevation="0">
				<div class="profile-card__head">
					<v-avatar
						size="80"
						color="primary"
						class="profile-card__avatar"
					>
						<v-img v-if="user.avatar" :src="user.avatar" />
						<span v-else class="profile-card__initials">
							{{ getInitials(user.name) }}
						</span>
					</v-avatar>

					<div class="profile-card__identity">
						<h1 class="profile-card__name">{{ user.name }}</h1>
						<div class="profile-card__email">
							<v-icon size="16" start>mdi-email-outline</v-icon>
							{{ user.email }}
						</div>
						<div class="profile-card__registered">
							<v-icon size="14" start>mdi-clock-outline</v-icon>
							На сервисе с {{ formatDateShort(user.registeredAt) }}
						</div>
					</div>
				</div>

				<!--
					Метрики. Используем сетку из трёх карточек — на узких
					экранах сворачиваются в одну колонку.
				-->
				<div class="profile-card__stats">
					<div
						v-for="stat in stats"
						:key="stat.key"
						class="stat-tile"
					>
						<v-icon size="22" :color="stat.color">{{ stat.icon }}</v-icon>
						<div class="stat-tile__value">{{ stat.value }}</div>
						<div class="stat-tile__label">{{ stat.label }}</div>
					</div>
				</div>
			</v-card>

			<!--
				========================================================
				СРЕДНЯЯ ЗОНА — 2 колонки: настройки слева, табы справа.
				На <960px — одна колонка сверху вниз.
				========================================================
			-->
			<div class="profile-page__body">
				<!-- ============ Левая колонка: настройки ============ -->
				<aside class="profile-page__settings-col">
					<v-card class="settings-card" elevation="0">
						<h2 class="settings-card__title">Настройки профиля</h2>

						<v-text-field
							v-model="form.name"
							label="Имя"
							variant="outlined"
							density="comfortable"
							hide-details
							class="mb-3"
						/>

						<v-text-field
							v-model="form.email"
							label="Email"
							type="email"
							variant="outlined"
							density="comfortable"
							hide-details
							class="mb-3"
						/>

						<!--
							Предпочтительные жанры — мультиселект с чипами.
							closable-chips позволяет удалять выбранные жанры
							прямо из инпута.
						-->
						<v-autocomplete
							v-model="form.preferredGenres"
							:items="mockGenreOptions"
							label="Предпочтительные жанры"
							variant="outlined"
							density="comfortable"
							multiple
							chips
							closable-chips
							hide-details
							class="mb-4"
						/>

						<v-btn
							color="primary"
							block
							:disabled="!isFormDirty"
							@click="saveProfile"
						>
							Сохранить изменения
						</v-btn>

						<v-divider class="my-4" />

						<v-btn
							variant="outlined"
							color="error"
							block
							prepend-icon="mdi-logout"
							@click="logout"
						>
							Выйти из аккаунта
						</v-btn>
					</v-card>
				</aside>

				<!-- ============ Правая колонка: табы ============ -->
				<section class="profile-page__tabs-col">
					<v-tabs
						v-model="activeTab"
						color="primary"
						show-arrows
						class="profile-page__tabs"
					>
						<v-tab value="recommendations">Рекомендации</v-tab>
						<v-tab value="favorites">Избранное</v-tab>
						<v-tab value="history">История</v-tab>
						<v-tab value="interactions">Взаимодействия</v-tab>
					</v-tabs>

					<v-window v-model="activeTab" class="profile-page__window">
						<!-- ============ Вкладка: Рекомендации ============ -->
						<v-window-item value="recommendations">
							<BookCarousel
								title="Рекомендуем вам"
								:books="recommendationsForYou"
								view-all-to="/recommendations?type=for-you"
							/>
							<BookCarousel
								title="По вашим жанрам"
								:books="recommendationsByGenres"
								view-all-to="/recommendations?type=by-genres"
							/>
							<BookCarousel
								title="Похожие на просмотренные"
								:books="recommendationsSimilar"
								view-all-to="/recommendations?type=similar"
							/>
						</v-window-item>

						<!-- ============ Вкладка: Избранное ============ -->
						<v-window-item value="favorites">
							<template v-if="favorites.length">
								<v-row dense>
									<v-col
										v-for="book in favorites"
										:key="book.id"
										cols="12"
										sm="6"
										md="4"
									>
										<!--
											Обёртка нужна, чтобы крестик «убрать»
											позиционировался поверх BookCard. Сам
											BookCard переиспользуем без правок.
										-->
										<div class="favorite-tile">
											<BookCard :book="book" />
											<v-btn
												icon
												size="x-small"
												variant="flat"
												color="black"
												class="favorite-tile__remove"
												aria-label="Убрать из избранного"
												@click.stop.prevent="removeFavorite(book.id)"
											>
												<v-icon size="16">mdi-close</v-icon>
											</v-btn>
										</div>
									</v-col>
								</v-row>
							</template>

							<EmptyState
								v-else
								icon="mdi-heart-off-outline"
								text="В избранном пока нет книг"
								action-text="Перейти в каталог"
								action-to="/catalog"
							/>
						</v-window-item>

						<!-- ============ Вкладка: История просмотров ============ -->
						<v-window-item value="history">
							<template v-if="history.length">
								<div class="d-flex justify-end mb-3">
									<v-btn
										variant="outlined"
										size="small"
										color="error"
										prepend-icon="mdi-delete-outline"
										@click="clearHistory"
									>
										Очистить историю
									</v-btn>
								</div>

								<!--
									Каждая группа = одна «корзина» по времени.
									Пустые группы скрываем, чтобы заголовок не висел один.
								-->
								<div
									v-for="group in historyGroups"
									v-show="group.items.length"
									:key="group.key"
									class="history-group"
								>
									<h3 class="history-group__title">{{ group.title }}</h3>
									<div class="history-group__list">
										<NuxtLink
											v-for="item in group.items"
											:key="item.id + item.viewedAt"
											:to="`/book/${item.id}`"
											class="history-item"
										>
											<v-img
												:src="item.cover"
												:aspect-ratio="2/3"
												cover
												class="history-item__cover"
											/>
											<div class="history-item__info">
												<div class="history-item__title">{{ item.title }}</div>
												<div class="history-item__author">{{ item.author }}</div>
												<div class="history-item__viewed">
													{{ formatDateTime(item.viewedAt) }}
												</div>
											</div>
											<div class="history-item__rating">
												<v-icon size="14" color="amber">mdi-star</v-icon>
												{{ item.rating.toFixed(1) }}
											</div>
										</NuxtLink>
									</div>
								</div>
							</template>

							<EmptyState
								v-else
								icon="mdi-history"
								text="История просмотров пуста"
							/>
						</v-window-item>

						<!-- ============ Вкладка: Мои взаимодействия ============ -->
						<v-window-item value="interactions">
							<v-expansion-panels
								v-model="openInteractions"
								multiple
								variant="accordion"
								class="interactions-panels"
							>
								<!-- ===== Мои оценки ===== -->
								<v-expansion-panel value="ratings">
									<v-expansion-panel-title>
										Мои оценки ({{ userRatings.length }})
									</v-expansion-panel-title>
									<v-expansion-panel-text>
										<div
											v-for="r in userRatings"
											:key="r.bookId"
											class="rating-row"
										>
											<v-img
												:src="r.cover"
												:aspect-ratio="2/3"
												cover
												class="rating-row__cover"
											/>
											<div class="rating-row__info">
												<NuxtLink
													:to="`/book/${r.bookId}`"
													class="rating-row__title"
												>
													{{ r.title }}
												</NuxtLink>
												<div class="rating-row__author">{{ r.author }}</div>
												<div class="rating-row__date">
													Оценено {{ formatDateShort(r.ratedAt) }}
												</div>
											</div>
											<div class="rating-row__action">
												<!--
													Inline-редактирование: пока не нажата «Изменить»,
													показываем звёзды readonly. После — те же звёзды,
													но интерактивные. Сохранение — локальное.
												-->
												<v-rating
													v-model="r.userRating"
													:readonly="!r.editing"
													density="compact"
													color="amber"
													size="small"
												/>
												<v-btn
													variant="text"
													size="small"
													class="mt-1"
													@click="toggleRatingEdit(r)"
												>
													{{ r.editing ? 'Готово' : 'Изменить оценку' }}
												</v-btn>
											</div>
										</div>

										<EmptyState
											v-if="!userRatings.length"
											icon="mdi-star-outline"
											text="Вы пока не оценивали книги"
										/>
									</v-expansion-panel-text>
								</v-expansion-panel>

								<!-- ===== Мои отзывы ===== -->
								<v-expansion-panel value="reviews">
									<v-expansion-panel-title>
										Мои отзывы ({{ userReviews.length }})
									</v-expansion-panel-title>
									<v-expansion-panel-text>
										<div
											v-for="review in userReviews"
											:key="review.bookId"
											class="review-row"
										>
											<div class="review-row__head">
												<NuxtLink
													:to="`/book/${review.bookId}`"
													class="review-row__title"
												>
													{{ review.title }}
												</NuxtLink>
												<div class="review-row__date">
													{{ formatDateShort(review.reviewedAt) }}
												</div>
											</div>
											<v-rating
												:model-value="review.rating"
												readonly
												density="compact"
												color="amber"
												size="small"
											/>
											<p class="review-row__text">{{ review.text }}</p>
											<v-btn
												variant="text"
												size="small"
												:to="`/book/${review.bookId}`"
											>
												Перейти к книге
												<v-icon end>mdi-chevron-right</v-icon>
											</v-btn>
										</div>

										<EmptyState
											v-if="!userReviews.length"
											icon="mdi-message-outline"
											text="Вы пока не оставляли отзывов"
										/>
									</v-expansion-panel-text>
								</v-expansion-panel>

								<!-- ===== Добавления в избранное ===== -->
								<v-expansion-panel value="additions">
									<v-expansion-panel-title>
										Добавления в избранное ({{ favoriteAdditions.length }})
									</v-expansion-panel-title>
									<v-expansion-panel-text>
										<NuxtLink
											v-for="add in favoriteAdditions"
											:key="add.bookId + add.addedAt"
											:to="`/book/${add.bookId}`"
											class="addition-row"
										>
											<v-icon size="16" color="pink">mdi-heart</v-icon>
											<span class="addition-row__title">{{ add.title }}</span>
											<span class="addition-row__author">{{ add.author }}</span>
											<v-spacer />
											<span class="addition-row__date">
												{{ formatDateShort(add.addedAt) }}
											</span>
										</NuxtLink>

										<EmptyState
											v-if="!favoriteAdditions.length"
											icon="mdi-heart-outline"
											text="Здесь будут отображаться книги, которые вы добавляли в избранное"
										/>
									</v-expansion-panel-text>
								</v-expansion-panel>
							</v-expansion-panels>
						</v-window-item>
					</v-window>
				</section>
			</div>
		</v-container>

		<v-snackbar v-model="snackbar.show" :timeout="2500" color="success">
			{{ snackbar.text }}
		</v-snackbar>
	</div>
</template>

<script>
import { storeToRefs } from 'pinia';
import { useUserStore } from '~/stores/user';

import BookCard from '~/components/common/BookCard.vue';
import BookCarousel from '~/components/common/BookCarousel.vue';
import EmptyState from '~/components/common/EmptyState.vue';

// === MOCK DATA — убрать, когда бэкенд будет готов ===
import {
	mockUser,
	mockGenreOptions,
	mockFavorites,
	mockHistory,
	mockUserRatings,
	mockUserReviews,
	mockFavoriteAdditions,
	mockRecommendationsForYou,
	mockRecommendationsByGenres,
	mockRecommendationsSimilar
} from '~/mocks/profileMocks.js';

// Защита от анонимных пользователей реализована глобально через middleware.
// Это объявление подключает middleware/auth.ts именно к этой странице.
definePageMeta({ middleware: 'auth' });

export default {
	name: 'ProfilePage',

	components: { BookCard, BookCarousel, EmptyState },

	setup() {
		const userStore = useUserStore();
		const { isAuthenticated } = storeToRefs(userStore);
		return { userStore, isAuthenticated };
	},

	data() {
		return {
			mockGenreOptions,

			// Локальное состояние профиля. Стартует как копия mockUser —
			// чтобы правки в форме не мутировали моки напрямую.
			user: structuredClone(mockUser),

			// Редактируемая копия для формы.
			// При «Сохранить» — пишется обратно в user.
			form: {
				name: mockUser.name,
				email: mockUser.email,
				preferredGenres: [...mockUser.preferredGenres]
			},

			// Активная вкладка. По умолчанию — рекомендации, чтобы при
			// первом заходе пользователь видел контент, а не пустой список.
			activeTab: 'recommendations',

			// Списки во вкладках — копии моков, чтобы локальные «удаления»
			// и правки оценок не дёргали другие страницы при HMR.
			favorites: [...mockFavorites],
			history:   [...mockHistory],
			userRatings: mockUserRatings.map((r) => ({ ...r, editing: false })),
			userReviews: [...mockUserReviews],
			favoriteAdditions: [...mockFavoriteAdditions],

			recommendationsForYou:    mockRecommendationsForYou,
			recommendationsByGenres:  mockRecommendationsByGenres,
			recommendationsSimilar:   mockRecommendationsSimilar,

			openInteractions: ['ratings'],

			snackbar: { show: false, text: '' }
		};
	},

	computed: {
		// Метрики в карточке пользователя. Считаем из user.stats,
		// чтобы при изменении локально (например, removeFavorite) можно
		// было пересчитать.
		stats() {
			return [
				{ key: 'read',      icon: 'mdi-book-open-variant', color: 'primary',  value: this.user.stats.read,      label: 'книг прочитано' },
				{ key: 'favorites', icon: 'mdi-heart',             color: 'pink',     value: this.favorites.length,     label: 'в избранном' },
				{ key: 'reviews',   icon: 'mdi-message-text',      color: 'amber',    value: this.user.stats.reviews,   label: 'отзывов оставлено' }
			];
		},

		// Считаем форму «грязной», если хоть одно поле отличается от
		// сохранённого состояния пользователя. Иначе — дизейблим кнопку.
		isFormDirty() {
			if (this.form.name !== this.user.name) return true;
			if (this.form.email !== this.user.email) return true;
			if (this.form.preferredGenres.length !== this.user.preferredGenres.length) return true;
			return this.form.preferredGenres.some(
				(g) => !this.user.preferredGenres.includes(g)
			);
		},

		// Группировка истории по «корзинам» времени относительно «сегодня».
		// Заголовки идут в порядке: Сегодня / Вчера / На этой неделе / Ранее.
		historyGroups() {
			const today    = startOfDay(new Date());
			const yesterday = new Date(today); yesterday.setDate(yesterday.getDate() - 1);
			const weekAgo   = new Date(today); weekAgo.setDate(weekAgo.getDate() - 7);

			const groups = {
				today:    { key: 'today',    title: 'Сегодня',         items: [] },
				yesterday:{ key: 'yesterday',title: 'Вчера',           items: [] },
				week:     { key: 'week',     title: 'На этой неделе',  items: [] },
				earlier:  { key: 'earlier',  title: 'Ранее',           items: [] }
			};

			// Сортируем по дате просмотра по убыванию — новые сверху.
			const sorted = [...this.history].sort(
				(a, b) => new Date(b.viewedAt) - new Date(a.viewedAt)
			);

			for (const item of sorted) {
				const d = new Date(item.viewedAt);
				if (d >= today)               groups.today.items.push(item);
				else if (d >= yesterday)      groups.yesterday.items.push(item);
				else if (d >= weekAgo)        groups.week.items.push(item);
				else                          groups.earlier.items.push(item);
			}

			return [groups.today, groups.yesterday, groups.week, groups.earlier];
		}
	},

	methods: {
		// ====== действия профиля ======
		// TODO: PUT /api/profile/me { name, email, preferredGenres }
		saveProfile() {
			this.user.name = this.form.name;
			this.user.email = this.form.email;
			this.user.preferredGenres = [...this.form.preferredGenres];
			this.snackbar = { show: true, text: 'Профиль обновлён' };
		},

		// TODO: POST /api/auth/logout
		logout() {
			this.userStore.logout();
			this.$router.push('/login');
		},

		// ====== избранное ======
		// TODO: DELETE /api/favorites/:bookId
		removeFavorite(id) {
			this.favorites = this.favorites.filter((b) => b.id !== id);
		},

		// ====== история ======
		// TODO: DELETE /api/reading-history
		clearHistory() {
			this.history = [];
		},

		// ====== оценки ======
		// TODO: PUT /api/ratings/:bookId при выходе из режима редактирования.
		toggleRatingEdit(r) {
			r.editing = !r.editing;
		},

		// ====== вспомогательные ======
		getInitials(name) {
			if (!name) return '?';
			const parts = name.trim().split(/\s+/);
			return (parts[0][0] + (parts[1]?.[0] || '')).toUpperCase();
		},
		formatDateShort(iso) {
			return new Date(iso).toLocaleDateString('ru-RU', {
				day: '2-digit', month: 'long', year: 'numeric'
			});
		},
		formatDateTime(iso) {
			return new Date(iso).toLocaleString('ru-RU', {
				day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit'
			});
		}
	}
};

// Вспомогательная функция вне компонента: возвращает Date с обнулённым временем.
// Нужна для классификации истории просмотров по дням.
function startOfDay(d) {
	const x = new Date(d);
	x.setHours(0, 0, 0, 0);
	return x;
}
</script>

<style scoped lang="scss">
.profile-page {
	color: #fff;
	padding-top: 24px;
	padding-bottom: 60px;

	&__container {
		max-width: 1280px;
		margin: 0 auto;
		padding: 0 24px;
	}

	&__body {
		display: grid;
		grid-template-columns: minmax(0, 35%) minmax(0, 65%);
		gap: 24px;
		align-items: start;
		margin-top: 24px;

		@media (max-width: 960px) {
			grid-template-columns: 1fr;
		}
	}

	&__settings-col {
		position: sticky;
		top: 96px;

		@media (max-width: 960px) {
			position: static;
		}
	}

	&__tabs {
		margin-bottom: 16px;
	}

	&__window {
		// v-window клипует overflow — это ломает карусели со стрелками.
		// Возвращаем видимость, чтобы стрелки и тени карточек не обрезались.
		overflow: visible;
	}
}

// ============ Карточка пользователя ============
.profile-card {
	background: rgba(255, 255, 255, 0.04) !important;
	border: 1px solid rgba(255, 255, 255, 0.08);
	border-radius: 20px;
	padding: 24px;

	&__head {
		display: flex;
		align-items: center;
		gap: 20px;
		flex-wrap: wrap;
	}

	&__initials {
		font-size: 28px;
		font-weight: 700;
		color: #fff;
	}

	&__name {
		font-size: 26px;
		font-weight: 800;
		line-height: 1.2;
	}

	&__email,
	&__registered {
		font-size: 13px;
		color: rgba(255, 255, 255, 0.6);
		margin-top: 4px;
	}

	&__stats {
		display: grid;
		grid-template-columns: repeat(3, 1fr);
		gap: 12px;
		margin-top: 20px;

		@media (max-width: 600px) {
			grid-template-columns: 1fr;
		}
	}
}

.stat-tile {
	background: rgba(255, 255, 255, 0.05);
	border: 1px solid rgba(255, 255, 255, 0.08);
	border-radius: 14px;
	padding: 16px;
	text-align: center;

	&__value {
		font-size: 26px;
		font-weight: 800;
		margin-top: 6px;
		line-height: 1;
	}

	&__label {
		font-size: 12px;
		color: rgba(255, 255, 255, 0.6);
		margin-top: 4px;
	}
}

// ============ Настройки ============
.settings-card {
	background: rgba(255, 255, 255, 0.04) !important;
	border: 1px solid rgba(255, 255, 255, 0.08);
	border-radius: 20px;
	padding: 20px;

	&__title {
		font-size: 18px;
		font-weight: 700;
		margin-bottom: 16px;
	}
}

// ============ Избранное ============
.favorite-tile {
	position: relative;

	&__remove {
		position: absolute;
		top: 4px;
		left: 4px;
		background: rgba(0, 0, 0, 0.75) !important;
		color: #fff !important;
		z-index: 2;
	}
}

// ============ История ============
.history-group {
	margin-bottom: 24px;

	&__title {
		font-size: 14px;
		font-weight: 700;
		letter-spacing: 0.4px;
		text-transform: uppercase;
		color: rgba(255, 255, 255, 0.5);
		margin-bottom: 10px;
	}

	&__list {
		display: flex;
		flex-direction: column;
		gap: 8px;
	}
}

.history-item {
	display: flex;
	align-items: center;
	gap: 12px;
	padding: 8px 12px;
	background: rgba(255, 255, 255, 0.03);
	border: 1px solid rgba(255, 255, 255, 0.06);
	border-radius: 12px;
	color: #fff;
	text-decoration: none;
	transition: background 0.2s ease;

	&:hover {
		background: rgba(255, 255, 255, 0.07);
	}

	&__cover {
		width: 50px;
		height: 75px;
		flex-shrink: 0;
		border-radius: 6px;
		overflow: hidden;
		background: #2a2a2a;
	}

	&__info {
		flex: 1;
		min-width: 0;
	}

	&__title {
		font-size: 14px;
		font-weight: 600;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
	}

	&__author {
		font-size: 12px;
		color: rgba(255, 255, 255, 0.55);
	}

	&__viewed {
		font-size: 11px;
		color: rgba(255, 255, 255, 0.4);
		margin-top: 2px;
	}

	&__rating {
		display: flex;
		align-items: center;
		gap: 3px;
		font-size: 13px;
		font-weight: 700;
		color: rgba(255, 255, 255, 0.85);
	}
}

// ============ Взаимодействия ============
.interactions-panels {
	:deep(.v-expansion-panel) {
		background: rgba(255, 255, 255, 0.04) !important;
		color: #fff;
	}
	:deep(.v-expansion-panel-title) {
		font-weight: 600;
	}
}

.rating-row {
	display: flex;
	gap: 12px;
	align-items: center;
	padding: 10px 0;
	border-bottom: 1px solid rgba(255, 255, 255, 0.06);

	&:last-child { border-bottom: none; }

	&__cover {
		width: 50px;
		height: 75px;
		flex-shrink: 0;
		border-radius: 6px;
		overflow: hidden;
		background: #2a2a2a;
	}

	&__info {
		flex: 1;
		min-width: 0;
	}

	&__title {
		font-size: 14px;
		font-weight: 600;
		color: #fff;
		text-decoration: none;

		&:hover { text-decoration: underline; }
	}

	&__author {
		font-size: 12px;
		color: rgba(255, 255, 255, 0.55);
	}

	&__date {
		font-size: 11px;
		color: rgba(255, 255, 255, 0.4);
		margin-top: 2px;
	}

	&__action {
		text-align: right;
	}
}

.review-row {
	padding: 12px 0;
	border-bottom: 1px solid rgba(255, 255, 255, 0.06);

	&:last-child { border-bottom: none; }

	&__head {
		display: flex;
		justify-content: space-between;
		align-items: baseline;
		gap: 12px;
	}

	&__title {
		font-size: 15px;
		font-weight: 700;
		color: #fff;
		text-decoration: none;
	}

	&__date {
		font-size: 12px;
		color: rgba(255, 255, 255, 0.5);
	}

	&__text {
		font-size: 13px;
		color: rgba(255, 255, 255, 0.8);
		margin: 6px 0;
		display: -webkit-box;
		-webkit-line-clamp: 3;
		-webkit-box-orient: vertical;
		overflow: hidden;
	}
}

.addition-row {
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 8px 0;
	border-bottom: 1px solid rgba(255, 255, 255, 0.06);
	color: #fff;
	text-decoration: none;
	font-size: 14px;

	&:last-child { border-bottom: none; }

	&__title {
		font-weight: 600;
	}

	&__author {
		color: rgba(255, 255, 255, 0.55);
		font-size: 12px;
	}

	&__date {
		font-size: 12px;
		color: rgba(255, 255, 255, 0.5);
	}
}
</style>
