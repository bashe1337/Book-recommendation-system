<template>
	<v-app>
		<!--
			AppBar скрываем на лендинге для неавторизованного пользователя
			(чистый welcome-экран без навигации). На всех остальных страницах
			и для авторизованных — показываем как обычно.
		-->
		<AppBar v-if="showHeader" />
		<v-main>
			<slot></slot>
		</v-main>
	</v-app>
</template>

<script>
import { storeToRefs } from 'pinia';
import { useUserStore } from '~/stores/user';

import AppBar from '~/components/header/AppBar.vue';

export default {
	components: { AppBar },

	setup() {
		const route = useRoute();
		const userStore = useUserStore();
		const { isAuthenticated } = storeToRefs(userStore);

		// На клиенте восстанавливаем сессию из localStorage —
		// чтобы решение «показывать ли хэдер» сразу было корректным.
		if (import.meta.client) {
			userStore.restoreFromStorage();
		}

		return { route, isAuthenticated };
	},

	computed: {
		// Скрываем шапку:
		//   1) на лендинге «/» для гостя,
		//   2) на любом /onboarding/* — это фокус-экраны без навигации.
		showHeader() {
			const path = this.route.path;
			if (path.startsWith('/onboarding')) return false;
			const isLanding = path === '/';
			return !(isLanding && !this.isAuthenticated);
		}
	}
}
</script>

<style lang="scss">
</style>
