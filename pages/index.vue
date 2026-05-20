<template>
	<div class="home">
		<!--
			Hero «Рекомендация дня» — самый верх главной, сразу под хэдером.
			Данные подгружаются из моков (см. ~/mocks/homeMocks.js).
		-->
		<HeroDaily :book="heroBook" />

		<v-container class="home__container">
			<!--
				Публичные карусели — отображаются всем пользователям,
				в том числе неавторизованным.
			-->
			<BookCarousel
				title="Популярное"
				:books="popularBooks"
				view-all-to="/catalog?filter=popular"
			/>

			<BookCarousel
				title="Новинки"
				:books="newBooks"
				view-all-to="/catalog?filter=new"
			/>

			<BookCarousel
				title="Рейтинг"
				:books="topRatedBooks"
				view-all-to="/catalog?filter=top-rated"
			/>

			<BookCarousel
				title="Выбор редакции"
				:books="editorsChoice"
				view-all-to="/catalog?filter=editors-choice"
			/>

			<!--
				Персональные карусели — только для авторизованных пользователей.
				Признак авторизации берётся из Pinia-стора useUserStore.
				Используем v-if (а не v-show), чтобы DOM не содержал лишних
				секций, когда пользователь не залогинен.
			-->
			<template v-if="isAuthenticated">
				<BookCarousel
					title="Рекомендуем вам"
					:books="forYouBooks"
					view-all-to="/catalog?filter=for-you"
				/>

				<BookCarousel
					title="По вашим жанрам"
					:books="byGenresBooks"
					view-all-to="/catalog?filter=by-genres"
				/>

				<BookCarousel
					title="Похожие на просмотренные"
					:books="similarViewedBooks"
					view-all-to="/catalog?filter=similar-viewed"
				/>
			</template>
		</v-container>
	</div>
</template>

<script>
import { storeToRefs } from 'pinia';
import { useUserStore } from '~/stores/user';

import HeroDaily from '~/components/common/HeroDaily.vue';
import BookCarousel from '~/components/common/BookCarousel.vue';

// === MOCK DATA — заменить на реальные эндпоинты когда бэкенд будет готов ===
import {
	heroBook,
	popularBooks,
	newBooks,
	topRatedBooks,
	editorsChoice,
	forYouBooks,
	byGenresBooks,
	similarViewedBooks
} from '~/mocks/homeMocks.js';

export default {
	name: 'HomePage',

	components: {
		HeroDaily,
		BookCarousel
	},

	setup() {
		// Признак авторизации — наружу отдаём как реактивный ref.
		// Это позволяет в шаблоне писать просто isAuthenticated.
		const userStore = useUserStore();
		const { isAuthenticated } = storeToRefs(userStore);

		return {
			isAuthenticated,
			// все секции — из mock-файла
			heroBook,
			popularBooks,
			newBooks,
			topRatedBooks,
			editorsChoice,
			forYouBooks,
			byGenresBooks,
			similarViewedBooks
		};
	}

	// TODO: когда бэкенд будет готов, заменить mock-импорты на параллельные
	// fetch-запросы по эндпоинтам, перечисленным в ~/mocks/homeMocks.js.
}
</script>

<style scoped lang="scss">
.home {
	padding-top: 10px;
	padding-bottom: 60px;

	&__container {
		max-width: 1440px;
		margin: 0 auto;
		padding: 0 100px;
	}

	@media (max-width: 960px) {
		&__container { padding: 0 24px; }
	}

	@media (max-width: 600px) {
		&__container { padding: 0 16px; }
	}
}
</style>
