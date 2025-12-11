<template>
	<div class="catalog-page">
		<v-container class="catalog-container">
			<h1 class="text-h4 font-weight-bold mb-6">Каталог книг</h1>

			<v-row>
				<v-col cols="12" md="9" lg="9">
					<div class="d-flex align-center justify-space-between mb-6">
						<div style="width: 200px">
							<v-select
								v-model="sortBy"
								:items="sortOptions"
								label="Сортировка"
								density="compact"
								variant="outlined"
								hide-details
							/>
						</div>
					</div>
					<div class="catalog-grid">
						<BookCard 
							v-for="book in books" 
							:key="book.id" 
							:book="book" 
						/>
					</div>
					<div class="d-flex justify-center mt-8">
						<v-pagination 
							v-model="page" 
							:length="10" 
							rounded="circle"
						/>
					</div>
				</v-col>
				<v-col cols="12" md="3" lg="3">
					<CatalogFilters 
						@apply="applyFilters" 
						@reset="resetFilters" 
					/>
				</v-col>
			</v-row>
		</v-container>
	</div>
</template>

<script>
import CatalogFilters from '~/components/catalog/CatalogFilters'
import BookCard from '~/components/common/BookCard'

export default {
	name: 'CatalogPage',
	components: {
		CatalogFilters,
		BookCard
	},
	
	async setup() {
		const { searchBooks } = useBooks()
		const { data: books } = await useAsyncData('catalog-books', () => 
			searchBooks('subject:fiction', 12)
		)
		
		return { books }
	},

	data() {
		return {
			page: 1,
			sortBy: 'popular',
			sortOptions: [
				{ title: 'По популярности', value: 'popular' },
				{ title: 'По новизне', value: 'newest' },
				{ title: 'По алфавиту', value: 'alpha' }
			]
		}
	},

	methods: {
		applyFilters(filters) {
			console.log('Применяем фильтры:', filters)
			// Тут будет логика перезапроса книг с фильтрами
		},
		resetFilters() {
			console.log('Сброс фильтров')
		}
	}
}
</script>

<style scoped lang="scss">
.catalog-container {
	max-width: 1440px;
	margin-top: 20px;
}

.catalog-grid {
	display: grid;
	grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
	gap: 24px;
	
	@media (max-width: 600px) {
		grid-template-columns: repeat(2, 1fr);
		gap: 16px;
	}
}
</style>
