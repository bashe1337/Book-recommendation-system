<template>
	<v-app-bar
		elevation="0"
		:class="['header', { 'header--scrolled': isScrolled }]"
		height="64"
	>
		<v-container
			fluid
			class="header__container"
		>
			<div class="header__logo">
				<NuxtLink to="/" class="header__logo-link">
				<div class="header__logo-text">BRS</div>
				</NuxtLink>
			</div>

			<!--
				Навигация. Элементы с authOnly: true рендерятся только
				для авторизованных пользователей.
			-->
			<div class="header__nav">
				<v-btn
					v-for="item in visibleMenuItems"
					:key="item.title"
					:to="item.to"
					variant="text"
					class="header__nav-item"
				>
				<v-icon v-if="item.icon" start size="small">{{ item.icon }}</v-icon>
					{{ item.title }}
				</v-btn>
			</div>

			<v-spacer />

			<!--
				Поиск.
				Заменили v-text-field на иконочную кнопку:
				по клику — переход на /search.
				TODO: при необходимости реализовать модальный поиск
				      поверх страницы — обсудить позже.
			-->
			<div class="header__actions d-flex align-center ml-4">
				<v-btn
					icon
					variant="text"
					size="small"
					class="header__action-btn header__search-btn"
					aria-label="Поиск"
					@click="goToSearch"
				>
					<v-icon>mdi-magnify</v-icon>
				</v-btn>
			</div>

			<!--
				Авторизованный пользователь: аватар + выпадающее меню (профиль/выход).
				Гость: две кнопки «Войти» и «Регистрация».
			-->
			<template v-if="currentUser">
				<v-menu offset="10">
					<template #activator="{ props }">
						<v-btn
							v-bind="props"
							variant="text"
							class="header__user-btn ml-4"
						>
							<v-avatar size="32" color="primary" class="mr-2">
								<span class="header__user-initials">
									{{ userInitials }}
								</span>
							</v-avatar>
							<span class="header__user-name d-none d-sm-inline">
								{{ displayName }}
							</span>
							<v-icon end>mdi-chevron-down</v-icon>
						</v-btn>
					</template>

					<v-list density="compact" class="header__user-menu">
						<v-list-item to="/profile" prepend-icon="mdi-account-circle-outline">
							<v-list-item-title>Профиль</v-list-item-title>
						</v-list-item>
						<v-list-item
							to="/recommendations"
							prepend-icon="mdi-star-outline"
						>
							<v-list-item-title>Рекомендации</v-list-item-title>
						</v-list-item>
						<v-divider />
						<v-list-item
							prepend-icon="mdi-logout"
							@click="onLogout"
						>
							<v-list-item-title>Выйти</v-list-item-title>
						</v-list-item>
					</v-list>
				</v-menu>
			</template>

			<template v-else>
				<v-btn
					variant="text"
					class="header__guest-btn ml-2 d-none d-sm-flex"
					to="/login"
				>
					Войти
				</v-btn>
				<v-btn
					color="white"
					class="header__auth-btn ml-2"
					elevation="0"
					to="/register"
				>
					Регистрация
				</v-btn>
			</template>

			<v-btn
				icon
				class="header__burger d-md-none ml-2"
				@click="drawer = !drawer"
			>
				<v-icon>mdi-menu</v-icon>
			</v-btn>
		</v-container>
	</v-app-bar>

	<!-- Мобильное меню -->
	<v-navigation-drawer
		v-model="drawer"
		location="right"
		temporary
		class="header__drawer"
	>
		<v-list density="compact" class="header__drawer-list pa-4">
		<v-list-item
			v-for="item in visibleMenuItems"
			:key="item.title"
			:to="item.to"
			link
			rounded="lg"
			class="header__drawer-item mb-2"
		>
			<template #prepend>
			<v-icon v-if="item.icon">{{ item.icon }}</v-icon>
			</template>
			<v-list-item-title>{{ item.title }}</v-list-item-title>
		</v-list-item>

		<v-divider class="my-4" />

		<!--
			В мобильном drawer тоже заменили поле ввода на кнопку
			«Поиск», ведущую на /search — единое поведение по приложению.
		-->
		<v-btn
			block
			variant="tonal"
			prepend-icon="mdi-magnify"
			class="header__drawer-search mb-4"
			@click="goToSearch"
		>
			Поиск
		</v-btn>
		</v-list>
	</v-navigation-drawer>
	</template>

	<script>
	import { useTheme } from 'vuetify'
	import { storeToRefs } from 'pinia'
	import { useUserStore } from '~/stores/user'
	import { useAuth } from '~/composables/useAuth.js'

	export default {
	name: 'AppHeader',

	setup() {
		// Тёмная тема зафиксирована: переключатель удалён, но Vuetify
		// по умолчанию открывает light — выставляем dark здесь.
		const theme = useTheme()
		theme.global.name.value = 'dark';

		// Реактивный isAuthenticated/user из Pinia — после login/logout
		// хэдер сам перерисует кнопки и пункты меню.
		const userStore = useUserStore()
		const { user, isAuthenticated } = storeToRefs(userStore)
		const auth = useAuth()
		return { theme, user, isAuthenticated, auth, userStore }
	},

	data() {
		return {
		drawer: false,
		isScrolled: false,
		// Пункт «Топ 100» удалён.
		// authOnly: true — рендерим только для авторизованных пользователей.
		menuItems: [
			{ title: 'Каталог', to: '/catalog' },
			{ title: 'Рекомендации', to: '/recommendations', authOnly: true }
		]
		}
	},

	computed: {
		// Текущий пользователь приходит из Pinia-стора и реактивен.
		// UserDto: { id, email, username, role, preferredGenreIds }.
		currentUser() {
			return this.user || null
		},

		// Имя для отображения: используем username (как с бэка).
		displayName() {
			return this.currentUser?.username || this.currentUser?.name || ''
		},

		userInitials() {
			const name = this.displayName
			const parts = name.trim().split(/\s+/)
			return ((parts[0]?.[0] || '?') + (parts[1]?.[0] || '')).toUpperCase()
		},

		// Скрываем authOnly-пункты от анонимов.
		visibleMenuItems() {
			return this.menuItems.filter(
				(item) => !item.authOnly || !!this.currentUser
			)
		}
	},

	mounted() {
		window.addEventListener('scroll', this.handleScroll)
		// Подтягиваем сессию из localStorage в стор при первом рендере.
		this.userStore?.restoreFromStorage?.()
	},

	beforeUnmount() {
		window.removeEventListener('scroll', this.handleScroll)
	},

	methods: {
		handleScroll() {
			this.isScrolled = window.scrollY > 20
		},

		goToSearch() {
			this.drawer = false
			this.$router.push('/search')
		},

		// POST /api/auth/revoke + локальная очистка стора и localStorage.
		async onLogout() {
			await this.auth.logout()
			this.$router.push('/login')
		}
	}
}
</script>

<style scoped lang="scss">
.header {
	background: rgba(18, 18, 18, 0.6) !important;
	border-bottom: #252525 solid 1px;
	backdrop-filter: blur(100px);
	-webkit-backdrop-filter: blur(10px);
	position: fixed !important;
	top: 0;
	left: 0;
	right: 0;
	width: 100%;
	z-index: 1000;
	transition: all 0.4s cubic-bezier(0.4, 0, 0.2, 1);
	margin-top: 0;
	border-radius: 0;
}

.header--scrolled {
	margin: 16px 50px 0 50px;
	width: auto !important;
	left: 0;
	right: 0;
	background: rgba(0, 0, 0, 0.7) !important;
	border-radius: 24px !important;
	border: 1px solid rgba(255, 255, 255, 0.1);
	box-shadow: 0 10px 40px rgba(0, 0, 0, 0.4);
	border-bottom: none;
	backdrop-filter: saturate(180%) blur(5px);
	-webkit-backdrop-filter: saturate(180%) blur(5px);
}

.header__container {
	display: flex;
	align-items: center;
	padding-inline: 50px;
}

.header__logo-link {
	display: flex;
	align-items: center;
	gap: 12px;
	text-decoration: none;
}

.header__logo-text {
	font-size: 28px;
	font-weight: 900;
	letter-spacing: 3px;
	background: linear-gradient(135deg, #ffffff 0%, #ffffff 100%);
	-webkit-background-clip: text;
	-webkit-text-fill-color: transparent;
	background-clip: text;
	transition: all 0.3s ease;

	.header__logo-link:hover & {
		letter-spacing: 5px;
	}
}

.header__nav {
	display: flex;
	gap: 10px;
	margin-left: 30px;
}

.header__nav-item {
	font-size: 16px !important;
	font-weight: 500;
	text-transform: none;
	letter-spacing: 0.5px;
	color: #ffffff !important;
	transition: all 0.3s ease;
	position: relative;
	border-radius: 30px;
	display: flex;
	gap: 10px;

	&::after {
		content: '';
		position: absolute;
		bottom: 8px;
		left: 50%;
		transform: translateX(-50%) scaleX(0);
		width: 80%;
		height: 2px;
		background: linear-gradient(90deg, #ffffff, #ffffff);
		transition: transform 0.3s ease;
		border-radius: 2px;
	}

	&:hover {
		color: #ffffff !important;
	}
}

.header__actions {
	gap: 4px;
}

.header__action-btn {
	color: rgba(255, 255, 255, 0.7) !important;
	transition: all 0.3s ease;

	// &:hover {
	// 	color: #ff6b35 !important;
	// 	transform: scale(1.1);
	// }
}

.header__user-btn {
	text-transform: none;
	font-weight: 600;
	color: #fff !important;
	border-radius: 999px;
}

.header__user-initials {
	font-size: 12px;
	font-weight: 700;
	color: #fff;
}

.header__user-name {
	font-size: 14px;
}

.header__guest-btn {
	text-transform: none;
	color: #fff !important;
}

.header__auth-btn {
	font-size: 16px !important;
	font-weight: 600;
	text-transform: none;
	letter-spacing: 0.5px;
	padding: 0 24px !important;
	height: 40px !important;
	transition: all 0.3s ease;
	background-color: #fdfdfd;
	color: #383838 !important;
	border-radius: 30px;

	&:hover {
		background-color: #efefef;
		color: #242424 !important;
		// border-radius: 10px;
	}
}

/* Адаптивность */
@media (max-width: 960px) {
.header__logo-text {
	font-size: 24px;
	letter-spacing: 2px;
}
}

@media (max-width: 600px) {
.header__auth-btn {
	font-size: 12px !important;
	padding: 0 16px !important;

	:deep(.v-icon) {
	display: none;
	}
}
}
</style>