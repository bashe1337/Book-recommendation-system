<template>
	<div class="profile-page">
		<v-container class="profile-page__container">
			<div class="profile-page__layout">
				<!--
					========================================================
					ЛЕВАЯ КОЛОНКА — карточка пользователя + блок «Настройки».
					========================================================
				-->
				<aside class="profile-page__sidebar">
					<!-- Большая карточка профиля -->
					<v-card class="profile-card" elevation="0">
						<div class="profile-card__avatar-row">
							<v-avatar
								size="92"
								class="profile-card__avatar"
								color="orange"
							>
								<span class="profile-card__avatar-initial">
									{{ initial }}
								</span>
							</v-avatar>

							<div class="profile-card__identity">
								<h1 class="profile-card__name">{{ displayName }}</h1>
								<div class="profile-card__handle">
									@{{ handle }}
									<span class="profile-card__dot">·</span>
									с {{ formatRegisteredAt(user.registeredAt) }}
								</div>
							</div>
						</div>

						<!--
							3 стат-плитки. Вместо «прочитано» показываем
							количество книг в избранном.
						-->
						<div class="profile-card__stats">
							<div class="stat-tile">
								<div class="stat-tile__value">{{ favoritesTotal }}</div>
								<div class="stat-tile__label">в избранном</div>
							</div>
							<div class="stat-tile">
								<div class="stat-tile__value">{{ ratingsCount }}</div>
								<div class="stat-tile__label">оценок</div>
							</div>
							<div class="stat-tile">
								<div class="stat-tile__value">{{ reviewsCount }}</div>
								<div class="stat-tile__label">отзывов</div>
							</div>
						</div>

						<!--
							Меты: любимый жанр + средняя оценка.
							Блок «Цель года» убран по требованию.
						-->
						<div class="profile-card__meta">
							<div class="meta-row">
								<div class="meta-row__label">Любимый жанр</div>
								<div class="meta-row__value">{{ favoriteGenre }}</div>
							</div>
							<div class="meta-row">
								<div class="meta-row__label">Средняя оценка</div>
								<div class="meta-row__value">
									{{ averageRating.toFixed(1) }}&nbsp;/&nbsp;5
								</div>
							</div>
						</div>

						<v-btn
							variant="outlined"
							block
							size="large"
							class="profile-card__edit-btn"
							to="/profile/edit"
						>
							Редактировать профиль
						</v-btn>
					</v-card>

					<!-- Блок «Настройки» -->
					<v-card class="settings-card" elevation="0">
						<div class="settings-card__title">Настройки</div>

						<v-list density="comfortable" class="settings-card__list">
							<v-list-item class="settings-card__item">
								<template #prepend>
									<v-icon size="20">mdi-account-outline</v-icon>
								</template>
								<v-list-item-title>Личные данные</v-list-item-title>
								<v-list-item-subtitle>{{ displayName }}</v-list-item-subtitle>
								<template #append>
									<v-icon size="16">mdi-chevron-right</v-icon>
								</template>
							</v-list-item>

							<v-list-item class="settings-card__item">
								<template #prepend>
									<v-icon size="20">mdi-email-outline</v-icon>
								</template>
								<v-list-item-title>Email</v-list-item-title>
								<v-list-item-subtitle>{{ user.email }}</v-list-item-subtitle>
								<template #append>
									<v-icon size="16">mdi-chevron-right</v-icon>
								</template>
							</v-list-item>

							<v-list-item class="settings-card__item">
								<template #prepend>
									<v-icon size="20">mdi-shape-outline</v-icon>
								</template>
								<v-list-item-title>Любимые жанры</v-list-item-title>
								<v-list-item-subtitle>
									{{ preferredGenreNames }}
								</v-list-item-subtitle>
								<template #append>
									<v-icon size="16">mdi-chevron-right</v-icon>
								</template>
							</v-list-item>

							<v-divider />

							<v-list-item class="settings-card__item" @click="onLogout">
								<template #prepend>
									<v-icon size="20" color="error">mdi-logout</v-icon>
								</template>
								<v-list-item-title class="text-error">Выйти из аккаунта</v-list-item-title>
							</v-list-item>
						</v-list>
					</v-card>
				</aside>

				<!--
					========================================================
					ПРАВАЯ КОЛОНКА — «Избранное» и «История чтения».
					========================================================
				-->
				<section class="profile-page__content">
					<!-- ===== Избранное ===== -->
					<div class="content-block">
						<div class="content-block__header">
							<div class="content-block__heading">
								<h2 class="content-block__title">Избранное</h2>
								<div class="content-block__caption">
									Книги, которые вы добавили в список «Хочу прочитать»
								</div>
							</div>
							<NuxtLink to="/profile?tab=favorites" class="content-block__link">
								Все ({{ favoritesTotal }})
							</NuxtLink>
						</div>

						<template v-if="favoritesVisible.length">
							<v-row dense>
								<v-col
									v-for="book in favoritesVisible"
									:key="book.id"
									cols="6"
									sm="4"
									md="2"
								>
									<BookCard :book="book" />
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
					</div>

					<!-- ===== История чтения ===== -->
					<div class="content-block">
						<div class="content-block__header">
							<div class="content-block__heading">
								<h2 class="content-block__title">История просмотра</h2>
								<div class="content-block__caption">
									Последние просмотренные книги
								</div>
							</div>
							<NuxtLink to="/profile?tab=history" class="content-block__link">
								Открыть полностью
							</NuxtLink>
						</div>

						<template v-if="historyVisible.length">
							<v-row dense>
								<v-col
									v-for="book in historyVisible"
									:key="book.id"
									cols="6"
									sm="4"
									md="2"
								>
									<BookCard :book="book" />
								</v-col>
							</v-row>
						</template>

						<EmptyState
							v-else
							icon="mdi-history"
							text="История просмотров пуста"
						/>
					</div>
				</section>
			</div>
		</v-container>
	</div>
</template>

<script>
import { storeToRefs } from 'pinia';
import { useUserStore } from '~/stores/user';
import { useAuth } from '~/composables/useAuth.js';

import BookCard from '~/components/common/BookCard.vue';
import EmptyState from '~/components/common/EmptyState.vue';
import { mapBookList, mapBookSummary } from '~/utils/bookAdapters.js';

definePageMeta({ middleware: 'auth' });

// Сколько карточек показываем в строке Избранного/Истории на этой странице.
const MAX_ROW = 5;

export default {
	name: 'ProfilePage',

	components: { BookCard, EmptyState },

	setup() {
		const userStore = useUserStore();
		const { user: storeUser } = storeToRefs(userStore);
		const auth = useAuth();
		const api = useApi();
		return { userStore, storeUser, auth, api };
	},

	data() {
		return {
			loading: true,

			// Списки книг — заполняются после fetch.
			favorites: [],
			history:   [],

			// Счётчики для верхних плиток. Берутся через PagedResult.totalCount.
			favoritesTotal: 0,
			ratingsCount:   0,   // прокси — totalCount отзывов пользователя
			reviewsCount:   0,

			// Справочник жанров (id → name) — чтобы показать «Любимый жанр» по имени.
			genresMap: {}
		};
	},

	computed: {
		// Текущий пользователь напрямую из Pinia. Если стор пуст — null
		// (middleware всё равно не пустит сюда без аутентификации).
		user() {
			return this.storeUser || {};
		},

		// Имя для отображения: на бэке поле называется username.
		displayName() {
			return this.user.username || 'Читатель';
		},

		handle() {
			const email = (this.user.email || '').toLowerCase();
			return email.split('@')[0] || 'reader';
		},

		initial() {
			return (this.displayName || '?').trim().charAt(0).toUpperCase();
		},

		// preferredGenreIds приходит с бэка как Guid[]. Показать имена можем,
		// только если справочник жанров уже загружен.
		preferredGenreNames() {
			const ids = this.user.preferredGenreIds || [];
			if (!ids.length) return 'Не выбраны';
			return ids
				.map((id) => this.genresMap[id])
				.filter(Boolean)
				.join(', ') || 'Загружаем…';
		},

		favoriteGenre() {
			const ids = this.user.preferredGenreIds || [];
			if (!ids.length) return '—';
			return this.genresMap[ids[0]] || '…';
		},

		// На бэке нет отдельной агрегации «средняя оценка пользователя».
		// Это поле — будущая фича. Пока показываем заглушку «—», чтобы
		// блок остался на месте дизайна.
		averageRating() { return 0; },

		favoritesVisible() {
			return this.favorites.slice(0, MAX_ROW);
		},
		historyVisible() {
			return this.history.slice(0, MAX_ROW);
		}
	},

	async mounted() {
		// На случай прямого захода по URL — подтянем актуальный профиль с бэка.
		await Promise.allSettled([
			this.loadMe(),
			this.loadGenres(),
			this.loadFavorites(),
			this.loadHistory(),
			this.loadReviews()
		]);
		this.loading = false;
	},

	methods: {
		// GET /api/users/me — может вернуть обновлённые preferredGenreIds.
		async loadMe() {
			try {
				const me = await this.api.get('/api/users/me');
				this.userStore.setUser(me);
			} catch { /* остаёмся со значением из стора */ }
		},

		// GET /api/genres → [{ id, name, slug }]. Кэшируем как id → name.
		async loadGenres() {
			try {
				const list = await this.api.get('/api/genres');
				this.genresMap = Object.fromEntries(
					(list || []).map((g) => [g.id, g.name])
				);
			} catch { this.genresMap = {}; }
		},

		// GET /api/users/me/favorites → PagedResult<FavoriteDto>
		// FavoriteDto: { id, bookId, addedAt, book: BookSummaryDto }
		async loadFavorites() {
			try {
				const res = await this.api.get('/api/users/me/favorites', {
					query: { page: 1, pageSize: MAX_ROW }
				});
				this.favoritesTotal = res?.totalCount ?? 0;
				this.favorites = (res?.items || [])
					.map((f) => mapBookSummary(f.book))
					.filter(Boolean);
			} catch {
				this.favorites = [];
				this.favoritesTotal = 0;
			}
		},

		// GET /api/users/me/history → PagedResult<ViewHistoryDto>
		// ViewHistoryDto: { id, bookId, viewedAt, book: BookSummaryDto }
		async loadHistory() {
			try {
				const res = await this.api.get('/api/users/me/history', {
					query: { page: 1, pageSize: MAX_ROW }
				});
				this.history = (res?.items || [])
					.map((h) => mapBookSummary(h.book))
					.filter(Boolean);
			} catch {
				this.history = [];
			}
		},

		// GET /api/users/me/reviews → PagedResult<ReviewDto> — берём totalCount
		// и как прокси-метрику для «оценок», и как количество «отзывов».
		async loadReviews() {
			try {
				const res = await this.api.get('/api/users/me/reviews', {
					query: { page: 1, pageSize: 1 }
				});
				const total = res?.totalCount ?? 0;
				this.reviewsCount = total;
				this.ratingsCount = total; // временно; добавим отдельный счётчик позже
			} catch { /* noop */ }
		},

		formatRegisteredAt(iso) {
			if (!iso) return '';
			const d = new Date(iso);
			return d.toLocaleDateString('ru-RU', {
				month: 'long',
				year: 'numeric'
			});
		},

		async onLogout() {
			await this.auth.logout();
			this.$router.push('/login');
		}
	}
};
</script>

<style scoped lang="scss">
.profile-page {
	color: #fff;
	padding-top: 24px;
	padding-bottom: 80px;

	&__container {
		max-width: 1440px;
		margin: 0 auto;
		padding: 0 24px;

		@media (min-width: 1280px) { padding: 0 40px; }
	}

	&__layout {
		display: grid;
		grid-template-columns: 340px 1fr;
		gap: 32px;
		align-items: start;

		@media (max-width: 960px) {
			grid-template-columns: 1fr;
			gap: 24px;
		}
	}

	&__sidebar {
		position: sticky;
		top: 96px;
		display: flex;
		flex-direction: column;
		gap: 16px;

		@media (max-width: 960px) { position: static; }
	}

	&__content {
		display: flex;
		flex-direction: column;
		gap: 40px;
		min-width: 0;
	}
}

// ============ Карточка профиля ============
.profile-card {
	background: rgba(255, 255, 255, 0.04) !important;
	border: 1px solid rgba(255, 255, 255, 0.08);
	border-radius: 20px;
	padding: 24px;

	&__avatar-row {
		display: flex;
		align-items: center;
		gap: 18px;
		margin-bottom: 22px;
	}

	&__avatar-initial {
		font-size: 36px;
		font-weight: 700;
		color: #fff;
	}

	&__name {
		font-size: 22px;
		font-weight: 800;
		line-height: 1.15;
	}

	&__handle {
		margin-top: 4px;
		font-size: 12px;
		color: rgba(255, 255, 255, 0.55);
	}

	&__dot {
		margin: 0 4px;
		opacity: 0.5;
	}

	// Три стат-плитки в ряд: фон чуть светлее карточки, центрированный текст.
	&__stats {
		display: grid;
		grid-template-columns: repeat(3, 1fr);
		gap: 8px;
		margin-bottom: 22px;
	}

	&__meta {
		margin-bottom: 22px;
	}

	&__edit-btn {
		text-transform: none;
		font-weight: 600;
		border-radius: 999px;
	}
}

.stat-tile {
	background: rgba(255, 255, 255, 0.045);
	border: 1px solid rgba(255, 255, 255, 0.06);
	border-radius: 12px;
	padding: 12px 8px;
	text-align: center;

	&__value {
		font-size: 22px;
		font-weight: 800;
		line-height: 1;
	}

	&__label {
		margin-top: 6px;
		font-size: 10.5px;
		text-transform: uppercase;
		letter-spacing: 0.5px;
		color: rgba(255, 255, 255, 0.5);
	}
}

.meta-row {
	display: flex;
	justify-content: space-between;
	align-items: baseline;
	padding: 8px 0;
	font-size: 13px;
	border-bottom: 1px dashed rgba(255, 255, 255, 0.06);

	&:last-child { border-bottom: none; }

	&__label {
		color: rgba(255, 255, 255, 0.6);
	}

	&__value {
		font-weight: 700;
		color: #fff;
	}

	&--goal {
		padding-bottom: 6px;
	}
}

// ============ Карточка настроек ============
.settings-card {
	background: rgba(255, 255, 255, 0.04) !important;
	border: 1px solid rgba(255, 255, 255, 0.08);
	border-radius: 20px;
	padding: 12px 8px 6px;

	&__title {
		font-size: 11px;
		text-transform: uppercase;
		letter-spacing: 0.6px;
		color: rgba(255, 255, 255, 0.5);
		padding: 6px 12px 4px;
	}

	&__list {
		background: transparent !important;
		padding: 0;
	}

	&__item {
		border-radius: 12px;
		padding-block: 8px;

		:deep(.v-list-item-title) {
			font-size: 14px;
			font-weight: 600;
		}

		:deep(.v-list-item-subtitle) {
			font-size: 12px;
			color: rgba(255, 255, 255, 0.55);
		}
	}
}

// ============ Контентный блок (Избранное / История) ============
.content-block {
	&__header {
		display: flex;
		align-items: flex-start;
		justify-content: space-between;
		gap: 16px;
		margin-bottom: 18px;
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

	&__link {
		font-size: 13px;
		font-weight: 600;
		color: rgb(var(--v-theme-primary));
		text-decoration: none;
		flex-shrink: 0;

		&:hover { text-decoration: underline; }
	}
}
</style>
