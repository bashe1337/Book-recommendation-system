import { defineStore } from 'pinia'

// Структура UserDto с бэкенда (см. backend/Brs.Application/DTOs/Auth/UserDto.cs)
export interface BackendUser {
	id: string                    // Guid
	email: string
	username: string
	role: string                  // 'User' | 'Admin'
	preferredGenreIds: string[]   // Guid[]
}

// Тонкий хелпер для localStorage — обёрнут try/catch,
// чтобы при отсутствии localStorage (SSR, приватный режим) не падать.
const ls = {
	get: (key: string): string | null => {
		if (typeof window === 'undefined') return null
		try { return window.localStorage.getItem(key) } catch { return null }
	},
	set: (key: string, val: string): void => {
		if (typeof window === 'undefined') return
		try { window.localStorage.setItem(key, val) } catch { /* noop */ }
	},
	remove: (key: string): void => {
		if (typeof window === 'undefined') return
		try { window.localStorage.removeItem(key) } catch { /* noop */ }
	}
}

const ACCESS_KEY = 'brs_access_token'
const REFRESH_KEY = 'brs_refresh_token'
const EXPIRES_KEY = 'brs_token_expires_at'
const USER_KEY = 'brs_user'

export const useUserStore = defineStore('user', {
	state: () => ({
		user: null as BackendUser | null,
		isAuthenticated: false,
		accessToken: null as string | null,
		refreshToken: null as string | null,
		// ISO-строка времени истечения access-токена (от бэка)
		expiresAt: null as string | null
	}),

	getters: {
		getUser: (state) => state.user,
		isLoggedIn: (state) => state.isAuthenticated
	},

	actions: {
		// Сохраняет полный AuthResponse от бэка: accessToken, refreshToken, expiresAt, user.
		setAuth(payload: {
			accessToken: string
			refreshToken: string
			expiresAt: string | Date
			user: BackendUser
		}): void {
			this.accessToken = payload.accessToken
			this.refreshToken = payload.refreshToken
			this.expiresAt = typeof payload.expiresAt === 'string'
				? payload.expiresAt
				: payload.expiresAt.toISOString()
			this.user = payload.user
			this.isAuthenticated = true

			ls.set(ACCESS_KEY, this.accessToken)
			ls.set(REFRESH_KEY, this.refreshToken)
			ls.set(EXPIRES_KEY, this.expiresAt)
			ls.set(USER_KEY, JSON.stringify(this.user))
		},

		// Обновление профиля без перевыпуска токенов (после PUT /api/users/me).
		setUser(user: BackendUser): void {
			this.user = user
			this.isAuthenticated = true
			ls.set(USER_KEY, JSON.stringify(user))
		},

		// Только access-токен (для случая ручного refresh без полного пакета).
		setAccessToken(token: string, expiresAt?: string): void {
			this.accessToken = token
			ls.set(ACCESS_KEY, token)
			if (expiresAt) {
				this.expiresAt = expiresAt
				ls.set(EXPIRES_KEY, expiresAt)
			}
		},

		logout(): void {
			this.user = null
			this.isAuthenticated = false
			this.accessToken = null
			this.refreshToken = null
			this.expiresAt = null

			ls.remove(ACCESS_KEY)
			ls.remove(REFRESH_KEY)
			ls.remove(EXPIRES_KEY)
			ls.remove(USER_KEY)
		},

		// Восстановление сессии из localStorage при первом рендере на клиенте.
		// Возвращает true, если что-то восстановили.
		restoreFromStorage(): boolean {
			const access = ls.get(ACCESS_KEY)
			const refresh = ls.get(REFRESH_KEY)
			const expiresAt = ls.get(EXPIRES_KEY)
			const userRaw = ls.get(USER_KEY)
			if (!access || !userRaw) return false

			try {
				this.user = JSON.parse(userRaw) as BackendUser
			} catch {
				return false
			}
			this.accessToken = access
			this.refreshToken = refresh
			this.expiresAt = expiresAt
			this.isAuthenticated = true
			return true
		},

		// === Совместимость со старым API стора ===
		// Чтобы не ломать места, где ещё дёргают эти методы.
		setToken(token: string): void {
			this.setAccessToken(token)
		},
		initAuth(): boolean {
			return this.restoreFromStorage()
		}
	}
})
