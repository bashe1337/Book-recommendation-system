<template>
	<!--
		Секция-карусель книг.
		Использует нативный overflow-x: auto (а не Swiper),
		чтобы стрелки прокрутки были встроены и не зависели от
		сторонней библиотеки.
		Если книг нет — секция не рендерится вообще.
	-->
	<section v-if="books.length" class="book-carousel">
		<div class="book-carousel__header">
			<h2 class="book-carousel__title">{{ title }}</h2>
			<v-btn
				v-if="viewAllTo"
				variant="text"
				color="white"
				:to="viewAllTo"
				class="book-carousel__more-btn"
			>
				Смотреть все
				<v-icon end>mdi-chevron-right</v-icon>
			</v-btn>
		</div>

		<div class="book-carousel__viewport">
			<!--
				Стрелки прокрутки. Видимы только на устройствах с указателем
				(hover) и достаточной ширине экрана — на мобильном
				пользователь скроллит пальцем.
			-->
			<v-btn
				icon
				size="small"
				variant="flat"
				class="book-carousel__arrow book-carousel__arrow--left"
				aria-label="Прокрутить влево"
				@click="scrollBy(-1)"
			>
				<v-icon>mdi-chevron-left</v-icon>
			</v-btn>

			<div ref="track" class="book-carousel__track">
				<div
					v-for="book in books"
					:key="book.id"
					class="book-carousel__item"
				>
					<BookCard :book="book" />
				</div>
			</div>

			<v-btn
				icon
				size="small"
				variant="flat"
				class="book-carousel__arrow book-carousel__arrow--right"
				aria-label="Прокрутить вправо"
				@click="scrollBy(1)"
			>
				<v-icon>mdi-chevron-right</v-icon>
			</v-btn>
		</div>
	</section>
</template>

<script>
import BookCard from '~/components/common/BookCard';

export default {
	name: 'BookCarousel',

	components: { BookCard },

	props: {
		title: { type: String, required: true },
		books: { type: Array, default: () => [] },
		// куда вести по «Смотреть все»; если не задано — кнопка не показывается
		viewAllTo: { type: String, default: '' }
	},

	methods: {
		// Прокрутка на ~80% ширины viewport-а в указанном направлении.
		// Использует scrollBy с поведением smooth, чтобы вписаться в общий feel.
		scrollBy(direction) {
			const track = this.$refs.track;
			if (!track) return;
			const delta = track.clientWidth * 0.8 * direction;
			track.scrollBy({ left: delta, behavior: 'smooth' });
		}
	}
}
</script>

<style scoped lang="scss">
.book-carousel {
	margin-bottom: 48px;

	&__header {
		display: flex;
		align-items: center;
		justify-content: space-between;
		margin-bottom: 24px;
	}

	&__title {
		font-size: 24px;
		font-weight: 700;
		letter-spacing: 0.5px;
		color: #fff;
	}

	&__more-btn {
		text-transform: none;
		font-weight: 600;
	}

	// Viewport карусели нужен только чтобы абсолютно позиционировать стрелки.
	&__viewport {
		position: relative;
	}

	// Сам трек — горизонтальный скролл без видимой полосы.
	&__track {
		display: flex;
		gap: 24px;
		overflow-x: auto;
		overflow-y: hidden;
		scroll-behavior: smooth;
		padding-bottom: 8px;
		scrollbar-width: none;

		&::-webkit-scrollbar {
			display: none;
		}
	}

	&__item {
		flex: 0 0 auto;
		width: 160px;

		@media (min-width: 960px) {
			width: 180px;
		}
	}

	&__arrow {
		position: absolute;
		top: 30%;
		z-index: 2;
		background: rgba(20, 20, 20, 0.85) !important;
		color: #fff !important;
		opacity: 0;
		transition: opacity 0.25s ease;
		box-shadow: 0 6px 20px rgba(0, 0, 0, 0.5);

		&--left { left: -16px; }
		&--right { right: -16px; }
	}

	// Стрелки появляются только на устройствах с указателем (десктоп),
	// чтобы не мешать тачскроллу на мобильном.
	@media (hover: hover) {
		&__viewport:hover &__arrow {
			opacity: 1;
		}
	}

	@media (max-width: 600px) {
		margin-bottom: 32px;

		&__title { font-size: 20px; }
		&__item { width: 140px; }
		&__arrow { display: none; }
	}
}
</style>
