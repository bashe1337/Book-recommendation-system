<template>
	<div class="auth-page">
		<v-card class="auth-card" elevation="0">
			<NuxtLink to="/" class="auth-card__logo">BRS</NuxtLink>

			<h1 class="auth-card__title">Создать аккаунт</h1>
			<div class="auth-card__subtitle">
				Расскажите немного о своих вкусах — и мы подберём книги под вас
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
				class="mb-2"
			/>

			<!--
				Любимые жанры — необязательное поле. closable-chips даёт
				удобное удаление прямо из инпута.
			-->
			<v-select
				v-model="form.preferredGenres"
				:items="genreOptions"
				label="Любимые жанры (необязательно)"
				variant="outlined"
				density="comfortable"
				multiple
				chips
				closable-chips
				class="mb-4"
			/>

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

// Список жанров для мультиселекта.
// TODO: при подключении бэкенда — GET /api/books/filters → genres
const GENRE_OPTIONS = [
	'Классика', 'Фантастика', 'Фэнтези', 'Детектив',
	'Антиутопия', 'Приключения', 'Исторический роман'
];

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
			genreOptions: GENRE_OPTIONS,

			form: {
				name: '',
				email: '',
				password: '',
				passwordConfirm: '',
				preferredGenres: []
			},

			showPassword: false,
			errorMessage: '',

			rules: {
				required:    (v) => !!v || 'Обязательное поле',
				minName:     (v) => !v || v.trim().length >= 2 || 'Минимум 2 символа',
				email:       (v) => !v || EMAIL_REGEX.test(v) || 'Некорректный email',
				minPassword: (v) => !v || v.length >= 6 || 'Минимум 6 символов'
			}
		};
	},

	computed: {
		// Уникальность email проверяем «на лету» — это и есть проверка
		// уникальности из ТЗ. На реальном бэке роль такой проверки сыграет
		// валидация на POST /api/auth/register.
		emailRules() {
			return [
				this.rules.required,
				this.rules.email,
				(v) => {
					if (!v) return true;
					if (!import.meta.client) return true;
					const users = JSON.parse(
						localStorage.getItem('mock_users') || '[]'
					);
					return !users.find((u) => u.email === v) ||
						'Этот email уже зарегистрирован';
				}
			];
		},

		// Связанное правило: пароли должны совпадать.
		// Возвращаем функцию-валидатор для подключения в v-text-field rules.
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
		// TODO: POST /api/auth/register — пока через useAuth + localStorage.
		onRegister() {
			this.errorMessage = '';
			if (!this.canSubmit) return;

			const result = this.auth.register({
				name: this.form.name.trim(),
				email: this.form.email.trim(),
				password: this.form.password,
				preferredGenres: this.form.preferredGenres
			});

			if (!result.success) {
				this.errorMessage = result.error || 'Не удалось зарегистрироваться';
				return;
			}

			// useAuth.register уже сохранил пользователя как «текущего»,
			// поэтому сразу ведём на главную.
			this.router.push('/');
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
