<template>
	<div class="onboarding">
		<div class="onboarding__container">
			<div class="onboarding__step">Шаг 1 из&nbsp;1</div>
			<h1 class="onboarding__title">Какие жанры вам нравятся?</h1>
			<p class="onboarding__subtitle">
				Выберите минимум <b>3 жанра</b> — это поможет нам подобрать
				книги под ваш вкус. Можно выбрать больше.
			</p>

			<div v-if="error" class="onboarding__error">
				<v-alert type="error" variant="tonal" density="compact">
					{{ error }}
				</v-alert>
			</div>

			<!-- Скелетон, пока тянем список жанров с бэка. -->
			<div v-if="loading" class="onboarding__loading">
				<v-progress-circular indeterminate color="primary" size="36" />
			</div>

			<!--
				Сетка чипов с жанрами с бэка (GET /api/genres).
				Активные = заливка primary. Идентификатор жанра — Guid,
				именно его шлём в PUT /api/users/me.
			-->
			<div v-else class="onboarding__chips">
				<button
					v-for="g in genres"
					:key="g.id"
					type="button"
					class="onboarding__chip"
					:class="{ 'onboarding__chip--active': selectedIds.includes(g.id) }"
					@click="toggle(g.id)"
				>
					{{ g.name }}
				</button>
			</div>

			<div class="onboarding__counter">
				Выбрано: <b>{{ selectedIds.length }}</b>
				<span v-if="!hasEnough && !loading" class="onboarding__counter-warn">
					&nbsp;— нужно ещё {{ MIN_GENRES - selectedIds.length }}
				</span>
			</div>

			<div class="onboarding__actions">
				<v-btn
					color="primary"
					size="x-large"
					class="onboarding__cta"
					:disabled="!hasEnough || saving"
					:loading="saving"
					@click="finish"
				>
					Продолжить
					<v-icon end>mdi-arrow-right</v-icon>
				</v-btn>
			</div>
		</div>
	</div>
</template>

<script>
import { storeToRefs } from 'pinia';
import { useUserStore } from '~/stores/user';

// Доступ только для авторизованных — экран вызывается сразу после
// успешной регистрации.
definePageMeta({ middleware: 'auth' });

const MIN_GENRES = 3;

export default {
	name: 'OnboardingGenresPage',

	setup() {
		const userStore = useUserStore();
		const { user } = storeToRefs(userStore);
		const api = useApi();
		return { userStore, user, api };
	},

	data() {
		return {
			MIN_GENRES,
			loading: true,
			saving: false,
			error: '',

			// Список жанров с бэка: [{ id, name, slug }]
			genres: [],

			// Выбранные id (Guid). Префилл из user.preferredGenreIds.
			selectedIds: [...(this.user?.preferredGenreIds || [])]
		};
	},

	computed: {
		hasEnough() {
			return this.selectedIds.length >= MIN_GENRES;
		}
	},

	async mounted() {
		// GET /api/genres — единый справочник жанров.
		try {
			const list = await this.api.get('/api/genres');
			this.genres = Array.isArray(list) ? list : [];
		} catch (err) {
			this.error = err?.message || 'Не удалось загрузить список жанров';
		} finally {
			this.loading = false;
		}
	},

	methods: {
		toggle(id) {
			const i = this.selectedIds.indexOf(id);
			if (i === -1) this.selectedIds.push(id);
			else this.selectedIds.splice(i, 1);
		},

		// PUT /api/users/me { username, preferredGenreIds[] }
		// username не меняем — отправляем текущий, чтобы пройти валидацию.
		async finish() {
			if (!this.hasEnough) return;

			this.saving = true;
			this.error = '';
			try {
				const updated = await this.api.put('/api/users/me', {
					username: this.user?.username || '',
					preferredGenreIds: [...this.selectedIds]
				});
				this.userStore.setUser(updated);
				this.$router.push('/');
			} catch (err) {
				this.error = err?.data?.message || err?.message || 'Не удалось сохранить';
			} finally {
				this.saving = false;
			}
		}
	}
};
</script>

<style scoped lang="scss">
.onboarding {
	min-height: 100vh;
	display: flex;
	align-items: center;
	justify-content: center;
	padding: 48px 24px;
	color: #fff;
	background:
		radial-gradient(ellipse at 20% 20%, rgba(245, 210, 107, 0.10) 0%, rgba(0,0,0,0) 50%),
		radial-gradient(ellipse at 80% 80%, rgba(184, 154, 255, 0.10) 0%, rgba(0,0,0,0) 55%);

	&__container {
		max-width: 760px;
		width: 100%;
		text-align: center;
	}

	&__step {
		font-size: 12px;
		font-weight: 600;
		letter-spacing: 0.4em;
		color: rgba(255, 255, 255, 0.55);
		text-transform: uppercase;
		margin-bottom: 16px;
	}

	&__title {
		font-size: 44px;
		font-weight: 800;
		line-height: 1.15;
		margin-bottom: 12px;

		@media (max-width: 600px) { font-size: 30px; }
	}

	&__subtitle {
		font-size: 16px;
		color: rgba(255, 255, 255, 0.7);
		max-width: 560px;
		margin: 0 auto 24px;
		line-height: 1.55;
	}

	&__error {
		max-width: 560px;
		margin: 0 auto 16px;
		text-align: left;
	}

	&__loading {
		padding: 40px 0;
	}

	&__chips {
		display: flex;
		flex-wrap: wrap;
		gap: 10px;
		justify-content: center;
		margin-bottom: 20px;
	}

	&__chip {
		appearance: none;
		background: rgba(255, 255, 255, 0.04);
		border: 1px solid rgba(255, 255, 255, 0.15);
		color: rgba(255, 255, 255, 0.85);
		padding: 12px 20px;
		border-radius: 999px;
		font-size: 15px;
		font-weight: 600;
		cursor: pointer;
		transition: all 0.18s ease;

		&:hover {
			background: rgba(255, 255, 255, 0.08);
			border-color: rgba(255, 255, 255, 0.3);
		}

		&--active {
			background: rgb(var(--v-theme-primary)) !important;
			border-color: rgb(var(--v-theme-primary));
			color: #1a1a1a !important;
		}
	}

	&__counter {
		font-size: 13px;
		color: rgba(255, 255, 255, 0.6);
		margin-bottom: 32px;
	}

	&__counter-warn {
		color: rgb(var(--v-theme-primary));
		font-weight: 600;
	}

	&__actions {
		display: flex;
		align-items: center;
		justify-content: center;
		gap: 16px;
		flex-wrap: wrap;
	}

	&__cta {
		text-transform: none;
		font-weight: 600;
		font-size: 16px !important;
		min-width: 220px;
		height: 56px !important;
		border-radius: 999px;
	}
}
</style>
