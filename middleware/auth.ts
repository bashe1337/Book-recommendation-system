// Middleware защиты страниц от анонимных пользователей.
// Подключается через `definePageMeta({ middleware: 'auth' })` на странице.
//
// Источник правды для текущего пользователя — localStorage (current_user),
// его читает useAuth().restore(); параллельно прокидываем состояние в
// Pinia-стор, чтобы UI везде получал единый сигнал isAuthenticated.
import { useAuth } from '~/composables/useAuth.js';
import { useUserStore } from '~/stores/user';

export default defineNuxtRouteMiddleware(async (to) => {
	// На SSR localStorage недоступен — проверку откладываем на клиент.
	if (import.meta.server) return;

	const userStore = useUserStore();
	if (!userStore.isAuthenticated) {
		// Пробуем восстановить сессию из localStorage.
		useAuth().restore();
	}

	if (!userStore.isAuthenticated) {
		return navigateTo(`/login?redirect=${encodeURIComponent(to.fullPath)}`);
	}
});
