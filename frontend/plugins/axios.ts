import axios from 'axios'

export default defineNuxtPlugin((nuxtApp) => {
	const api = axios.create({
		baseURL: 'https://www.googleapis.com/books/v1',
		headers: {
			'Content-Type': 'application/json'
		}
	});

	api.interceptors.response.use(
		(response) => response,
		(error) => {
			console.error('Axios Error:', error)
			return Promise.reject(error)
		}
	);

	return {
		provide: {
			api: api
		}
	};
})