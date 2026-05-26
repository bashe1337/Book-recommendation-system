// Middleware защиты страниц от анонимных пользователей.
// Подключается через `definePageMeta({ middleware: 'auth' })` на странице.
//
// Источник правды — Pinia-стор. Состояние сохраняется в localStorage
// под ключами brs_access_token / brs_refresh_token / brs_user
// (см. stores/user.ts).
import { useUserStore } from '~/stores/user';

export default defineNuxtRouteMiddleware(async (to) => {
	// На SSR localStorage недоступен — проверку откладываем на клиент.
	if (import.meta.server) return;

	const userStore = useUserStore();
	if (!userStore.isAuthenticated) {
		// Пробуем восстановить сессию из localStorage.
		userStore.restoreFromStorage();
	}

	if (!userStore.isAuthenticated) {
		return navigateTo(`/login?redirect=${encodeURIComponent(to.fullPath)}`);
	}
});
