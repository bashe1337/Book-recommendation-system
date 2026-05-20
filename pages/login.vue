<template>
	<div class="auth-page">
		<v-card class="auth-card" elevation="0">
			<!-- Логотип сервиса -->
			<NuxtLink to="/" class="auth-card__logo">BRS</NuxtLink>

			<h1 class="auth-card__title">Вход в аккаунт</h1>
			<div class="auth-card__subtitle">
				Войдите, чтобы получать персональные рекомендации
			</div>

			<!--
				Ошибка авторизации.
				Показываем, если login() вернул success=false.
			-->
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
				v-model="form.email"
				label="Email"
				type="email"
				variant="outlined"
				density="comfortable"
				autocomplete="email"
				:rules="[rules.required, rules.email]"
				class="mb-2"
			/>

			<!--
				Пароль с переключателем видимости.
				appendInnerIcon реактивно меняется в зависимости от showPassword.
			-->
			<v-text-field
				v-model="form.password"
				label="Пароль"
				:type="showPassword ? 'text' : 'password'"
				variant="outlined"
				density="comfortable"
				autocomplete="current-password"
				:append-inner-icon="showPassword ? 'mdi-eye-off' : 'mdi-eye'"
				:rules="[rules.required]"
				class="mb-4"
				@click:append-inner="showPassword = !showPassword"
				@keyup.enter="onLogin"
			/>

			<v-btn
				color="primary"
				block
				size="large"
				:disabled="!canSubmit"
				@click="onLogin"
			>
				Войти
			</v-btn>

			<div class="auth-card__switch">
				Нет аккаунта?
				<NuxtLink to="/register" class="auth-card__switch-link">
					Зарегистрироваться
				</NuxtLink>
			</div>
		</v-card>
	</div>
</template>

<script>
import { useAuth } from '~/composables/useAuth.js';

// Страница доступна только анонимам — guest middleware редиректит
// залогиненных на /.
definePageMeta({ middleware: 'guest', layout: 'default' });

export default {
	name: 'LoginPage',

	setup() {
		const router = useRouter();
		const route = useRoute();
		const auth = useAuth();
		return { router, route, auth };
	},

	data() {
		return {
			form: { email: '', password: '' },
			showPassword: false,
			errorMessage: '',

			// Простые правила валидации — переиспользуем на обоих экранах auth.
			rules: {
				required: (v) => !!v || 'Обязательное поле',
				email:    (v) =>
					!v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v) || 'Некорректный email'
			}
		};
	},

	computed: {
		// Кнопка «Войти» активна только при заполненных полях и валидном email.
		canSubmit() {
			return (
				this.form.email.trim().length > 0 &&
				this.form.password.length > 0 &&
				/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.form.email)
			);
		}
	},

	methods: {
		// TODO: POST /api/auth/login — пока через useAuth + localStorage.
		onLogin() {
			this.errorMessage = '';
			if (!this.canSubmit) return;

			const result = this.auth.login(
				this.form.email.trim(),
				this.form.password
			);

			if (!result.success) {
				this.errorMessage = result.error || 'Не удалось войти';
				return;
			}

			// После успешного входа — учитываем ?redirect=... если он есть,
			// иначе ведём на главную.
			const redirect = (this.route.query.redirect || '/').toString();
			this.router.push(redirect);
		}
	}
};
</script>

<style scoped lang="scss">
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
