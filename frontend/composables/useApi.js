// Единый клиент к .NET-бэкенду.
//
// Возможности:
//   • базовый URL берётся из runtimeConfig.public.apiBase;
//   • прозрачно добавляет Authorization: Bearer <accessToken>;
//   • при 401 один раз пытается обновить токен через /api/auth/refresh,
//     затем повторяет исходный запрос;
//   • выбрасывает Error со свойствами status / data, чтобы вызывающий
//     код мог различать «401 Unauthorized», «404 NotFound» и т. п.
//
// Использование:
//   const api = useApi();
//   const me = await api.get('/api/users/me');
//   await api.post('/api/books/<id>/ratings', { score: 5 });

import { useUserStore } from '~/stores/user';

// Глобальный promise рефреша — чтобы при параллельных 401 не было
// нескольких одновременных запросов /api/auth/refresh.
let refreshInFlight = null;

export const useApi = () => {
	const config = useRuntimeConfig();
	const baseUrl = config.public.apiBase;

	const userStore = useUserStore();

	// Низкоуровневая «оболочка» над $fetch. Делает 1 запрос.
	// Получает уже подготовленные headers/options.
	const rawFetch = async (path, options = {}) => {
		const url = path.startsWith('http') ? path : `${baseUrl}${path}`;
		return await $fetch(url, options);
	};

	// Сборка заголовков с токеном (если есть).
	const buildHeaders = (extra = {}) => {
		const headers = {
			Accept: 'application/json',
			...extra
		};
		if (userStore.accessToken) {
			headers.Authorization = `Bearer ${userStore.accessToken}`;
		}
		return headers;
	};

	// Один раз пытается обновить access-токен через refresh.
	// Если получилось — возвращает true. Иначе разлогинивает и возвращает false.
	const tryRefresh = async () => {
		if (!userStore.refreshToken) return false;
		if (!refreshInFlight) {
			refreshInFlight = (async () => {
				try {
					const res = await rawFetch('/api/auth/refresh', {
						method: 'POST',
						headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
						body: { refreshToken: userStore.refreshToken }
					});
					// AuthResponse: accessToken, refreshToken, expiresAt, user
					userStore.setAuth({
						accessToken: res.accessToken,
						refreshToken: res.refreshToken,
						expiresAt: res.expiresAt,
						user: res.user
					});
					return true;
				} catch {
					userStore.logout();
					return false;
				} finally {
					// Сбрасываем после небольшой паузы, чтобы параллельные запросы
					// успели подождать общий промис.
					setTimeout(() => { refreshInFlight = null; }, 0);
				}
			})();
		}
		return await refreshInFlight;
	};

	// Универсальный запрос с авто-refresh.
	const request = async (method, path, { body, query, headers, raw = false } = {}) => {
		const init = {
			method,
			headers: buildHeaders({
				...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
				...(headers || {})
			})
		};
		if (body !== undefined) init.body = body;
		if (query !== undefined) init.query = query;

		try {
			return await rawFetch(path, init);
		} catch (err) {
			const status = err?.response?.status ?? err?.status;

			// 401 — пробуем рефреш и повторяем исходный запрос (один раз).
			if (status === 401 && userStore.refreshToken && path !== '/api/auth/refresh') {
				const ok = await tryRefresh();
				if (ok) {
					init.headers = buildHeaders({
						...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
						...(headers || {})
					});
					return await rawFetch(path, init);
				}
			}

			// Прокидываем ошибку с полезной информацией для UI.
			const enriched = new Error(err?.data?.message || err?.message || 'Request failed');
			enriched.status = status;
			enriched.data = err?.data;
			enriched.original = err;
			throw enriched;
		}
	};

	return {
		get:    (path, opts)        => request('GET',    path, opts),
		post:   (path, body, opts)  => request('POST',   path, { ...opts, body }),
		put:    (path, body, opts)  => request('PUT',    path, { ...opts, body }),
		patch:  (path, body, opts)  => request('PATCH',  path, { ...opts, body }),
		del:    (path, opts)        => request('DELETE', path, opts),
		baseUrl
	};
};
