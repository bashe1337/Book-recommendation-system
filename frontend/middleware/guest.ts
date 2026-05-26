// Обратная защита: страница доступна ТОЛЬКО анонимным пользователям.
// Подключается на /login и /register через
// `definePageMeta({ middleware: 'guest' })`.
// Если пользователь уже залогинен — отправляем на главную.
import { useUserStore } from '~/stores/user';

export default defineNuxtRouteMiddleware(() => {
	if (import.meta.server) return;

	const userStore = useUserStore();
	if (!userStore.isAuthenticated) userStore.restoreFromStorage();

	if (userStore.isAuthenticated) {
		return navigateTo('/');
	}
});
