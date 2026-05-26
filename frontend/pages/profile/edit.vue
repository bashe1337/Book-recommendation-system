<template>
	<div class="profile-edit">
		<v-container class="profile-edit__container">
			<NuxtLink to="/profile" class="profile-edit__back">
				<v-icon size="18" start>mdi-arrow-left</v-icon>
				К&nbsp;профилю
			</NuxtLink>

			<h1 class="profile-edit__title">Редактирование профиля</h1>
			<p class="profile-edit__subtitle">
				Имя и&nbsp;любимые жанры — то, что используется для подбора рекомендаций.
				Email на&nbsp;текущей версии не редактируется.
			</p>

			<v-card class="profile-edit__card" elevation="0">
				<v-alert
					v-if="error"
					type="error"
					variant="tonal"
					density="compact"
					class="mb-4"
				>
					{{ error }}
				</v-alert>

				<v-alert
					v-if="saved"
					type="success"
					variant="tonal"
					density="compact"
					class="mb-4"
				>
					Изменения сохранены
				</v-alert>

				<v-text-field
					v-model="form.username"
					label="Имя"
					variant="outlined"
					density="comfortable"
					:rules="[rules.required, rules.minName]"
					class="mb-2"
				/>

				<v-text-field
					:model-value="user?.email || ''"
					label="Email"
					type="email"
					variant="outlined"
					density="comfortable"
					readonly
					hint="Изменение email временно недоступно"
					persistent-hint
					class="mb-4"
				/>

				<div class="profile-edit__section-label">Любимые жанры</div>
				<div class="profile-edit__section-hint">
					Выберите минимум 3 жанра — это поможет точнее рекомендовать книги
				</div>

				<div v-if="loading" class="text-center py-4">
					<v-progress-circular indeterminate color="primary" size="28" />
				</div>

				<div v-else class="profile-edit__chips">
					<v-chip
						v-for="g in genres"
						:key="g.id"
						:variant="form.preferredGenreIds.includes(g.id) ? 'flat' : 'outlined'"
						:color="form.preferredGenreIds.includes(g.id) ? 'primary' : 'grey-lighten-1'"
						class="profile-edit__chip"
						@click="toggleGenre(g.id)"
					>
						{{ g.name }}
					</v-chip>
				</div>

				<div class="profile-edit__selected-count">
					Выбрано: <b>{{ form.preferredGenreIds.length }}</b>
					<span
						v-if="form.preferredGenreIds.length < MIN_GENRES"
						class="profile-edit__warning"
					>
						(нужно ещё {{ MIN_GENRES - form.preferredGenreIds.length }})
					</span>
				</div>

				<v-divider class="my-5" />

				<div class="profile-edit__actions">
					<v-btn variant="text" to="/profile">
						Отмена
					</v-btn>
					<v-btn
						color="primary"
						size="large"
						:disabled="!canSubmit || saving"
						:loading="saving"
						@click="save"
					>
						Сохранить изменения
					</v-btn>
				</div>
			</v-card>
		</v-container>
	</div>
</template>

<script>
import { storeToRefs } from 'pinia';
import { useUserStore } from '~/stores/user';

definePageMeta({ middleware: 'auth' });

const MIN_GENRES = 3;

export default {
	name: 'ProfileEditPage',

	setup() {
		const userStore = useUserStore();
		const { user } = storeToRefs(userStore);
		const api = useApi();
		return { userStore, user, api };
	},

	data() {
		const u = this.$pinia?.state.value?.user?.user || null;
		return {
			MIN_GENRES,
			loading: true,
			saving: false,
			saved: false,
			error: '',

			genres: [],
			form: {
				username: u?.username || '',
				preferredGenreIds: [...(u?.preferredGenreIds || [])]
			},

			rules: {
				required: (v) => !!v || 'Обязательное поле',
				minName:  (v) => !v || v.trim().length >= 1 || 'Минимум 1 символ'
			}
		};
	},

	computed: {
		canSubmit() {
			return (
				this.form.username.trim().length >= 1 &&
				this.form.preferredGenreIds.length >= MIN_GENRES
			);
		}
	},

	async mounted() {
		// При прямом заходе по URL пользователь может быть только что
		// восстановлен — подстраховываемся актуальным значением из стора.
		if (this.user) {
			this.form.username = this.user.username || '';
			this.form.preferredGenreIds = [...(this.user.preferredGenreIds || [])];
		}

		try {
			const list = await this.api.get('/api/genres');
			this.genres = Array.isArray(list) ? list : [];
		} catch (err) {
			this.error = err?.message || 'Не удалось загрузить жанры';
		} finally {
			this.loading = false;
		}
	},

	methods: {
		toggleGenre(id) {
			const i = this.form.preferredGenreIds.indexOf(id);
			if (i === -1) this.form.preferredGenreIds.push(id);
			else this.form.preferredGenreIds.splice(i, 1);
		},

		// PUT /api/users/me { username, preferredGenreIds }
		async save() {
			if (!this.canSubmit) return;
			this.saving = true;
			this.error = '';
			this.saved = false;
			try {
				const updated = await this.api.put('/api/users/me', {
					username: this.form.username.trim(),
					preferredGenreIds: [...this.form.preferredGenreIds]
				});
				this.userStore.setUser(updated);
				this.saved = true;
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
.profile-edit {
	color: #fff;
	padding-top: 24px;
	padding-bottom: 80px;

	&__container {
		max-width: 720px;
		margin: 0 auto;
		padding: 0 24px;
	}

	&__back {
		display: inline-flex;
		align-items: center;
		gap: 4px;
		font-size: 13px;
		font-weight: 600;
		color: rgba(255, 255, 255, 0.65);
		text-decoration: none;
		margin-bottom: 16px;

		&:hover { color: #fff; }
	}

	&__title {
		font-size: 36px;
		font-weight: 800;
		letter-spacing: 0.2px;
		line-height: 1.1;

		@media (max-width: 600px) { font-size: 28px; }
	}

	&__subtitle {
		margin-top: 8px;
		font-size: 14px;
		color: rgba(255, 255, 255, 0.6);
		margin-bottom: 24px;
	}

	&__card {
		background: rgba(255, 255, 255, 0.04) !important;
		border: 1px solid rgba(255, 255, 255, 0.08);
		border-radius: 20px;
		padding: 24px;
	}

	&__section-label {
		font-size: 14px;
		font-weight: 700;
		margin-bottom: 4px;
	}

	&__section-hint {
		font-size: 12px;
		color: rgba(255, 255, 255, 0.55);
		margin-bottom: 12px;
	}

	&__chips {
		display: flex;
		flex-wrap: wrap;
		gap: 8px;
		margin-bottom: 12px;
	}

	&__chip {
		cursor: pointer;
		font-weight: 600;
	}

	&__selected-count {
		font-size: 12px;
		color: rgba(255, 255, 255, 0.55);
	}

	&__warning {
		color: rgb(var(--v-theme-primary));
		margin-left: 4px;
		font-weight: 600;
	}

	&__actions {
		display: flex;
		justify-content: flex-end;
		gap: 12px;
	}
}
</style>
