<template>
	<div class="home">
		<section class="home__section home__section--slider">
			<div class="home__slider-wrapper">
				<Swiper
					:modules="modules"
					:slides-per-view="'auto'"
					:space-between="24"
					:loop="true"
					:autoplay="{
						delay: 3000,
						disableOnInteraction: false,
						pauseOnMouseEnter: true
					}"
					:mousewheel="{ forceToAxis: true }"
					:grab-cursor="true"
					class="home__swiper"
				>
					<SwiperSlide
						v-for="book in popularBooks"
						:key="book.id"
						class="home__slide"
					>
						<BookCard :book="book" />
					</SwiperSlide>
				</Swiper>
			</div>
		</section>
		<v-container class="home__container">
			<section class="home__section">
				<div class="home__section-header">
					<h2 class="home__title">Новинки</h2>
					<v-btn
						variant="text"
						color="white"
						to="/catalog?sort=new"
						class="home__more-btn"
					>
						Все
						<v-icon end>mdi-chevron-right</v-icon>
					</v-btn>
				</div>
				<div class="home__slider-wrapper">
					<Swiper
						class="home__swiper"
						:modules="modules"
						:slides-per-view="'auto'"
						:space-between="24"
						:mousewheel="{ forceToAxis: true }"
						:grab-cursor="true"
					>
						<SwiperSlide
							v-for="book in popularBooks"
							:key="book.id"
							class="home__slide"
						>
							<BookCard :book="book" />
						</SwiperSlide>
					</Swiper>
				</div>
			</section>

			<section class="home__section">
				<div class="home__section-header">
					<h2 class="home__title">Сейчас читают</h2>
				</div>
				<div class="home__slider-wrapper">
					<Swiper
						class="home__swiper"
						:modules="modules"
						:slides-per-view="'auto'"
						:space-between="24"
						:mousewheel="{ forceToAxis: true }"
						:grab-cursor="true"
					>
						<SwiperSlide
							v-for="book in readingNowBooks"
							:key="book.id"
							class="home__slide"
						>
							<BookCard :book="book" />
						</SwiperSlide>
					</Swiper>
				</div>
			</section>
			<section class="home__section">
				<div class="home__section-header">
					<h2 class="home__title">Подборки жанров для вас</h2>
				</div>
				<!-- <div class="home__slider-wrapper">
					<Swiper
						class="home__swiper"
						:modules="modules"
						:slides-per-view="'auto'"
						:space-between="24"
						:mousewheel="{ forceToAxis: true }"
						:grab-cursor="true"
					>
						<SwiperSlide
							v-for="book in readingNowBooks"
							:key="book.id"
							class="home__slide"
						>
							<BookCard :book="book" />
						</SwiperSlide>
					</Swiper>
				</div> -->
				<div class="text-later">Тут будут подборки по жанрам...</div>
			</section>
		</v-container>
	</div>
</template>

<script>
import { Swiper, SwiperSlide } from 'swiper/vue';
import { Autoplay, Mousewheel, Navigation } from 'swiper/modules';

import 'swiper/css';
import 'swiper/css/autoplay';
import 'swiper/css/navigation';

import BookCard from '~/components/common/BookCard';

export default {
	name: 'HomePage',

	components: {
		BookCard,
		Swiper,
		SwiperSlide
	},

	async setup() {
		const { searchBooks, getNewestBooks } = useBooks()

		const { data: loadedData, pending } = await useAsyncData('home-data', async () => {
			const [popular, news, reading] = await Promise.all([
				searchBooks('subject:Fiction', 15),
				getNewestBooks(),
				searchBooks('subject:science', 15)
			])
			return { popular, news, reading }
		})

		return {
			loadedData,
			pending,
			modules: [Autoplay, Mousewheel, Navigation]
		}
	},

	computed: {
		popularBooks() {
			return this.loadedData?.popular || []
		},
		newBooks() {
			return this.loadedData?.news || []
		},
		readingNowBooks() {
			return this.loadedData?.reading || []
		}
	}
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

	&__section {
		margin-bottom: 48px;
	}
	
	&__section-header {
		display: flex;
		align-items: center;
		justify-content: space-between;
		margin-bottom: 24px;
	}

	&__title {
		font-size: 24px;
		font-weight: 700;
		letter-spacing: 0.5px;
	}

	&__more-btn {
		text-transform: none;
		font-weight: 600;
	}

	&__slider-wrapper {
		width: 100%;
		overflow: hidden;
	}

	&__swiper {
		width: 100%;
		padding-right: 16px;
		padding-bottom: 20px;
	}

	&__slide {
		// width: 100px !important;

		@media (min-width: 960px) {
			width: 180px !important;
		}
	}

	.swiper-wrapper {
		width: 100%;
	}

	&__grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
		gap: 24px;
		
		.v-skeleton-loader {
			width: 100%;
			border-radius: 12px;
			background: rgba(255,255,255,0.05);
		}

		@media (max-width: 600px) {
			grid-template-columns: repeat(2, 1fr);
			gap: 16px;
		}
	}

	.text-later {
		display: flex;
		justify-content: center;
		align-items: center;
		padding-block: 20px;
	}
}
</style>
