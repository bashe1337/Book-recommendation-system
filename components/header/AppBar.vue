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

			<div class="header__nav">
				<v-btn
					v-for="item in menuItems"
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

			<div class="header__search d-none d-sm-flex">
				<v-text-field
					v-model="searchQuery"
					placeholder="Поиск..."
					variant="solo"
					density="compact"
					hide-details
					single-line
					class="header__search-field"
					prepend-inner-icon="mdi-magnify"
					@keyup.enter="onSearch"
					@focus="searchFocused = true"
					@blur="searchFocused = false"
				>
				<template #append-inner>
					<v-fade-transition>
						<v-icon
							v-if="searchQuery"
							size="small"
							class="header__search-clear"
							@click="searchQuery = ''"
						>
							mdi-close-circle
						</v-icon>
					</v-fade-transition>
				</template>
				</v-text-field>
			</div>

			<div class="header__actions d-flex align-center ml-4">
				<v-btn
					icon
					variant="text"
					size="small"
					class="header__action-btn"
					@click="toggleTheme"
				>
				<v-icon>
					{{ isDark ? 'mdi-theme-light-dark' : 'mdi-theme-light-dark' }}
				</v-icon>
				</v-btn>

				<v-btn
					icon
					variant="text"
					size="small"
					class="header__action-btn d-none d-sm-flex"
				>
				<v-badge dot color="error">
					<v-icon>mdi-bell-outline</v-icon>
				</v-badge>
				</v-btn>
			</div>

			<v-btn
				color="white"
				class="header__auth-btn ml-4"
				elevation="0"
				@click="goToAuth"
			>
				Вход/Регистрация
			</v-btn>

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
			v-for="item in menuItems"
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

		<v-text-field
			v-model="searchQuery"
			placeholder="Поиск..."
			variant="outlined"
			density="compact"
			hide-details
			prepend-inner-icon="mdi-magnify"
			class="header__drawer-search mb-4"
			@keyup.enter="onSearch"
		/>
		</v-list>
	</v-navigation-drawer>
	</template>

	<script>
	import { useTheme } from 'vuetify'

	export default {
	name: 'AppHeader',

	setup() {
		const theme = useTheme()
		theme.global.name.value = 'dark';
		return { theme }
	},

	data() {
		return {
		searchQuery: '',
		searchFocused: false,
		drawer: false,
		isScrolled: false,
		menuItems: [
			{ title: 'Каталог', to: '/catalog' },
			{ title: 'Рекомендации для вас', to: '/recommendations' },
			{ title: 'Топ 100', to: '/top' }
		]
		}
	},

	computed: {
		isDark() {
			return this.theme.global.current.value.dark
		}
	},

	mounted() {
		window.addEventListener('scroll', this.handleScroll)
	},

	beforeUnmount() {
		window.removeEventListener('scroll', this.handleScroll)
	},

	methods: {
		handleScroll() {
			this.isScrolled = window.scrollY > 20
		},

		toggleTheme() {
			const current = this.theme.global.name.value;
			const next = current === 'dark' ? 'light' : 'dark';
			this.theme.global.name.value = next;
		},

		onSearch() {
			if (this.searchQuery.trim()) {
				this.$router.push(`/search?q=${encodeURIComponent(this.searchQuery)}`)
				this.drawer = false
			}
		},

		goToAuth() {
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

.header__search {
	max-width: 400px;
	width: 100%;
}

.header__search-field {
	:deep(.v-field) {
		background: rgba(255, 255, 255, 0.06);
		border-radius: 24px;
		font-size: 14px;
		transition: all 0.3s ease;

		// &:hover {
		// background: rgba(255, 255, 255, 0.1);
		// }

		&.v-field--focused {
		background: rgba(255, 255, 255, 0.12);
		box-shadow: 0 0 0 2px rgba(255, 107, 53, 0.3);
		}
	}

	:deep(.v-field__input) {
		padding: 8px 16px;
		min-height: 40px;
		color: rgba(255, 255, 255, 0.9);
	}

	:deep(.v-field__prepend-inner) {
		padding-top: 8px;
		color: rgba(255, 255, 255, 0.5);
	}

	:deep(input::placeholder) {
		color: rgba(255, 255, 255, 0.4);
		opacity: 1;
	}
}

.header__search-clear {
	cursor: pointer;
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
.header__search {
	max-width: 180px;
}

.header__auth-btn {
	font-size: 12px !important;
	padding: 0 16px !important;

	:deep(.v-icon) {
	display: none;
	}
}
}
</style>