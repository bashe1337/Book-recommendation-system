import { defineStore } from 'pinia'

export const useUserStore = defineStore('user', {
	state: () => ({
		user: null as any,
		isAuthenticated: false,
		token: null as string | null
	}),
	
	getters: {
		getUser: (state) => state.user,
		isLoggedIn: (state) => state.isAuthenticated
	},
	
	actions: {
		setUser(user: any) {
		this.user = user
		this.isAuthenticated = true
		},
		
		setToken(token: string) {
		this.token = token
		if (import.meta.client) {
			localStorage.setItem('token', token)
		}
		},
		
		logout() {
		this.user = null
		this.isAuthenticated = false
		this.token = null
		if (import.meta.client) {
			localStorage.removeItem('token')
		}
		},
		
		// Восстановление сессии при перезагрузке
		async initAuth() {
		if (import.meta.client) {
			const token = localStorage.getItem('token')
			if (token) {
			this.token = token
			// Здесь будет запрос к API для проверки токена
			// Пока что просто ставим isAuthenticated = true
			this.isAuthenticated = true
			}
		}
		}
	}
})
