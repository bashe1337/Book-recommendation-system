// Composable аутентификации поверх реального .NET-бэка.
//
// Эндпоинты (см. backend/Brs.Api/Controllers/AuthController.cs):
//   POST /api/auth/register   { email, password, username } -> AuthResponse
//   POST /api/auth/login      { email, password }           -> AuthResponse
//   POST /api/auth/refresh    { refreshToken }              -> AuthResponse (через useApi)
//   POST /api/auth/revoke                                   -> 204 (требует Authorization)
//
// AuthResponse: { accessToken, refreshToken, expiresAt, user }
//   user: UserDto { id, email, username, role, preferredGenreIds[] }
//
// Все запросы идут через useApi() — он сам прицепляет Bearer и обрабатывает 401→refresh.

import { useUserStore } from '~/stores/user';

export const useAuth = () => {
	const userStore = useUserStore();
	const api = useApi();

	// POST /api/auth/register — после регистрации бэк сразу возвращает токены.
	const register = async ({ name, email, password }) => {
		try {
			const res = await api.post('/api/auth/register', {
				email,
				password,
				username: name
			});
			userStore.setAuth(res);
			return { success: true, user: res.user };
		} catch (err) {
			return {
				success: false,
				error: extractError(err, 'Не удалось зарегистрироваться')
			};
		}
	};

	// POST /api/auth/login
	const login = async (email, password) => {
		try {
			const res = await api.post('/api/auth/login', { email, password });
			userStore.setAuth(res);
			return { success: true, user: res.user };
		} catch (err) {
			return {
				success: false,
				error: extractError(err, 'Неверный email или пароль')
			};
		}
	};

	// POST /api/auth/revoke + локальная очистка состояния.
	const logout = async () => {
		try {
			if (userStore.accessToken) {
				await api.post('/api/auth/revoke');
			}
		} catch {
			// игнорируем сетевые ошибки — токен на клиенте всё равно стираем
		}
		userStore.logout();
	};

	// Геттеры (для совместимости со старым API composable).
	const getCurrentUser = () => userStore.user;
	const isAuthenticated = () => userStore.isAuthenticated;

	// Восстановление сессии из localStorage — вызывается из layout
	// при первой загрузке на клиенте, чтобы UI сразу видел залогиненного.
	const restore = () => userStore.restoreFromStorage();

	return {
		register,
		login,
		logout,
		restore,
		getCurrentUser,
		isAuthenticated
	};
};

// Из исключений бэка пытаемся достать читаемое сообщение.
// ExceptionHandlingMiddleware возвращает { message, ... } или ValidationException-формат.
const extractError = (err, fallback) => {
	const data = err?.data;
	if (typeof data === 'string') return data;
	return (
		data?.message ||
		data?.title ||
		data?.error ||
		err?.message ||
		fallback
	);
};
