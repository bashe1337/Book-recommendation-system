export const useBooks = () => {
	const config = useRuntimeConfig();
	const API_URL = 'https://www.googleapis.com/books/v1/volumes';

	/**
	 * Поиск книг по запросу (для каталога, поиска)
	 * @param query - поисковая фраза
	 * @param maxResults - количество книг
	 */
	const searchBooks = async (query: string, maxResults: number = 10) => {
		try {
			const { data } = await useFetch(API_URL, {
				query: {
					q: query,
					maxResults: maxResults,
					printType: 'books',
					langRestrict: 'ru',
					orderBy: 'relevance'
				}
			});
			return transformGoogleBooks(data.value);
		} catch (error) {
			console.error('Google Books API Error:', error);
			return [];
		}
	}


	const getNewestBooks = async () => {
		try {
			const { data } = await useFetch(API_URL, {
				query: {
					q: 'subject:fiction',
					orderBy: 'newest',
					maxResults: 6,
					langRestrict: 'ru'
				}
			});
			return transformGoogleBooks(data.value);
		} catch (error) {
			return [];
		}
	}

	const transformGoogleBooks = (data: any) => {
		if (!data || !data.items) return [];

		return data.items.map((item: any) => {
			const info = item.volumeInfo;
			return {
				id: item.id,
				title: info.title || 'Без названия',
				authors: info.authors || ['Неизвестный автор'],
				cover: info.imageLinks?.thumbnail?.replace('http:', 'https:')?.replace('&zoom=1', '&zoom=2'),
				rating: info.averageRating || 0,
				description: info.description || '',
				categories: info.categories || [],
				year: info.publishedDate?.substring(0, 4) || ''
			};
		});
	}

	return {
		searchBooks,
		getNewestBooks
	};
}