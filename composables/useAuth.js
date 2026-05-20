// Единый composable для управления состоянием авторизации.
//
// Сейчас всё хранится в localStorage:
//   mock_users    — массив всех зарегистрированных пользователей;
//   current_user  — текущий «залогиненный» (JSON).
//
// TODO: при подключении бэкенда:
//   - заменить mock_users/current_user на JWT-токен в localStorage;
//   - login()    → POST /api/auth/login    { email, password }
//   - register() → POST /api/auth/register { name, email, password, preferredGenres }
//   - logout()   → POST /api/auth/logout
//   - getCurrentUser() → GET /api/profile/me
//
// Composable также синхронизирует Pinia-стор useUserStore, чтобы
// существующие части UI (middleware auth, ProfilePage, AppBar)
// продолжали работать без правок.

import { useUserStore } from '~/stores/user';

const USERS_KEY = 'mock_users';
const CURRENT_KEY = 'current_user';

// Безопасный JSON.parse — никогда не бросает.
const safeParse = (raw, fallback) => {
	try { return JSON.parse(raw); } catch { return fallback; }
};

export const useAuth = () => {
	const userStore = useUserStore();

	// На SSR localStorage недоступен — оборачиваем все обращения.
	const isClient = () => typeof window !== 'undefined' && !!window.localStorage;

	const getUsers = () => {
		if (!isClient()) return [];
		return safeParse(localStorage.getItem(USERS_KEY) || '[]', []);
	};

	const saveUsers = (users) => {
		if (!isClient()) return;
		localStorage.setItem(USERS_KEY, JSON.stringify(users));
	};

	const getCurrentUser = () => {
		if (!isClient()) return null;
		return safeParse(localStorage.getItem(CURRENT_KEY) || 'null', null);
	};

	const isAuthenticated = () => getCurrentUser() !== null;

	// Кладёт пользователя в localStorage + Pinia-стор.
	// Без store бы middleware/auth.ts не увидел нового пользователя.
	const setCurrent = (user) => {
		if (!isClient()) return;
		localStorage.setItem(CURRENT_KEY, JSON.stringify(user));
		userStore.setUser(user);
		userStore.setToken('mock-token'); // TODO: реальный JWT
	};

	// TODO: POST /api/auth/login → { token, user }
	const login = (email, password) => {
		const users = getUsers();
		const user = users.find(
			(u) => u.email === email && u.password === password
		);
		if (!user) {
			return { success: false, error: 'Неверный email или пароль' };
		}
		setCurrent(user);
		return { success: true, user };
	};

	// TODO: POST /api/auth/register → { token, user }
	// ВАЖНО: пароли в реальной системе хэшируются (bcrypt/PBKDF2).
	// Здесь храним plaintext только для мока.
	const register = ({ name, email, password, preferredGenres = [] }) => {
		const users = getUsers();

		if (users.find((u) => u.email === email)) {
			return {
				success: false,
				error: 'Пользователь с таким email уже существует'
			};
		}

		const newUser = {
			id: Date.now(),
			name,
			email,
			password, // TODO: только хэш в продакшене
			preferredGenres,
			registeredAt: new Date().toISOString(),
			stats: { read: 0, favorites: 0, reviews: 0 }
		};

		users.push(newUser);
		saveUsers(users);
		setCurrent(newUser);
		return { success: true, user: newUser };
	};

	const logout = () => {
		if (isClient()) {
			localStorage.removeItem(CURRENT_KEY);
		}
		userStore.logout();
		// TODO: POST /api/auth/logout (инвалидация токена на сервере)
	};

	// Восстановление сессии при перезагрузке — вызывается из middleware
	// и AppBar.mounted(). Идемпотентно.
	const restore = () => {
		const u = getCurrentUser();
		if (u) {
			userStore.setUser(u);
			userStore.setToken('mock-token');
		}
		return u;
	};

	return {
		getCurrentUser,
		isAuthenticated,
		login,
		register,
		logout,
		restore
	};
};
