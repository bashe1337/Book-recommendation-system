<template>
	<div class="auth-page">
		<v-card class="auth-card" elevation="0">
			<NuxtLink to="/" class="auth-card__logo">BRS</NuxtLink>

			<h1 class="auth-card__title">Создать аккаунт</h1>
			<div class="auth-card__subtitle">
				После регистрации мы предложим выбрать любимые жанры,
				чтобы подобрать книги под ваш вкус
			</div>

			<v-alert
				v-if="errorMessage"
				type="error"
				variant="tonal"
				density="compact"
				class="mb-4"
			>
				{{ errorMessage }}
			</v-alert>

			<v-text-field
				v-model="form.name"
				label="Имя"
				variant="outlined"
				density="comfortable"
				autocomplete="name"
				:rules="[rules.required, rules.minName]"
				class="mb-2"
			/>

			<v-text-field
				v-model="form.email"
				label="Email"
				type="email"
				variant="outlined"
				density="comfortable"
				autocomplete="email"
				:rules="emailRules"
				class="mb-2"
			/>

			<v-text-field
				v-model="form.password"
				label="Пароль"
				:type="showPassword ? 'text' : 'password'"
				variant="outlined"
				density="comfortable"
				autocomplete="new-password"
				:append-inner-icon="showPassword ? 'mdi-eye-off' : 'mdi-eye'"
				:rules="[rules.required, rules.minPassword]"
				class="mb-2"
				@click:append-inner="showPassword = !showPassword"
			/>

			<v-text-field
				v-model="form.passwordConfirm"
				label="Повторите пароль"
				:type="showPassword ? 'text' : 'password'"
				variant="outlined"
				density="comfortable"
				autocomplete="new-password"
				:rules="[rules.required, passwordsMatch]"
				class="mb-4"
			/>

			<!--
				Любимые жанры из формы убраны. После регистрации
				пользователь попадает на /onboarding/genres, где обязательно
				выбирает минимум 3 жанра — это шаг подбора рекомендаций.
			-->

			<v-btn
				color="primary"
				block
				size="large"
				:disabled="!canSubmit"
				@click="onRegister"
			>
				Зарегистрироваться
			</v-btn>

			<div class="auth-card__switch">
				Уже есть аккаунт?
				<NuxtLink to="/login" class="auth-card__switch-link">
					Войти
				</NuxtLink>
			</div>
		</v-card>
	</div>
</template>

<script>
import { useAuth } from '~/composables/useAuth.js';

definePageMeta({ middleware: 'guest', layout: 'default' });

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export default {
	name: 'RegisterPage',

	setup() {
		const router = useRouter();
		const auth = useAuth();
		return { router, auth };
	},

	data() {
		return {
			form: {
				name: '',
				email: '',
				password: '',
				passwordConfirm: ''
			},

			showPassword: false,
			errorMessage: '',
			submitting: false,

			rules: {
				required:    (v) => !!v || 'Обязательное поле',
				minName:     (v) => !v || v.trim().length >= 2 || 'Минимум 2 символа',
				email:       (v) => !v || EMAIL_REGEX.test(v) || 'Некорректный email',
				minPassword: (v) => !v || v.length >= 6 || 'Минимум 6 символов'
			}
		};
	},

	computed: {
		// Уникальность email теперь проверяет бэкенд при POST /api/auth/register
		// и возвращает осмысленную ошибку — мы её покажем в v-alert.
		emailRules() {
			return [this.rules.required, this.rules.email];
		},

		passwordsMatch() {
			return (v) => v === this.form.password || 'Пароли не совпадают';
		},

		canSubmit() {
			const f = this.form;
			return (
				f.name.trim().length >= 2 &&
				EMAIL_REGEX.test(f.email) &&
				f.password.length >= 6 &&
				f.password === f.passwordConfirm
			);
		}
	},

	methods: {
		// POST /api/auth/register через useAuth.
		// Жанры на этом шаге не собираем — пользователь выберет их
		// на следующем экране /onboarding/genres.
		async onRegister() {
			this.errorMessage = '';
			if (!this.canSubmit || this.submitting) return;

			this.submitting = true;
			try {
				const result = await this.auth.register({
					name: this.form.name.trim(),
					email: this.form.email.trim(),
					password: this.form.password
				});

				if (!result.success) {
					this.errorMessage = result.error || 'Не удалось зарегистрироваться';
					return;
				}

				this.router.push('/onboarding/genres');
			} finally {
				this.submitting = false;
			}
		}
	}
};
</script>

<style scoped lang="scss">
// Стили совпадают с login.vue — оставляем дубликат намеренно,
// чтобы страницы оставались самодостаточными (без общего .scss).
.auth-page {
	min-height: calc(100vh - 64px);
	display: flex;
	align-items: center;
	justify-content: center;
	padding: 40px 16px;
}

.auth-card {
	width: 100%;
	max-width: 440px;
	background: rgba(255, 255, 255, 0.04) !important;
	border: 1px solid rgba(255, 255, 255, 0.08);
	border-radius: 20px;
	padding: 32px;
	color: #fff;

	&__logo {
		display: block;
		text-align: center;
		font-size: 24px;
		font-weight: 900;
		letter-spacing: 4px;
		color: #fff;
		text-decoration: none;
		margin-bottom: 16px;
	}

	&__title {
		font-size: 24px;
		font-weight: 800;
		text-align: center;
		margin-bottom: 6px;
	}

	&__subtitle {
		font-size: 13px;
		color: rgba(255, 255, 255, 0.6);
		text-align: center;
		margin-bottom: 20px;
	}

	&__switch {
		text-align: center;
		margin-top: 16px;
		font-size: 13px;
		color: rgba(255, 255, 255, 0.7);
	}

	&__switch-link {
		color: #fff;
		text-decoration: underline;
		margin-left: 4px;
	}
}
</style>
