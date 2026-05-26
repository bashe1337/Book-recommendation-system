<template>
	<!--
		Секция-карусель книг.
		Использует нативный overflow-x: auto, стрелки появляются на hover.
		Слот `caption` (или одноимённый проп) — серая строка под жирным
		заголовком: коротко поясняет, как составлена подборка
		(«Алгоритм SVD · персональные рекомендации» и т. п.).
	-->
	<section v-if="books.length" class="book-carousel">
		<div class="book-carousel__header">
			<div class="book-carousel__heading">
				<h2 class="book-carousel__title">{{ title }}</h2>
				<div v-if="caption" class="book-carousel__caption">{{ caption }}</div>
			</div>
			<v-btn
				v-if="viewAllTo"
				variant="text"
				color="primary"
				:to="viewAllTo"
				class="book-carousel__more-btn"
			>
				Смотреть все
				<v-icon end>mdi-chevron-right</v-icon>
			</v-btn>
		</div>

		<div class="book-carousel__viewport">
			<v-btn
				icon
				size="small"
				variant="flat"
				class="book-carousel__arrow book-carousel__arrow--left"
				aria-label="Прокрутить влево"
				@click="scrollByDir(-1)"
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
				@click="scrollByDir(1)"
			>
				<v-icon>mdi-chevron-right</v-icon>
			</v-btn>
		</div>
	</section>
</template>

<script>
import BookCard from '~/components/common/BookCard.vue';

export default {
	name: 'BookCarousel',

	components: { BookCard },

	props: {
		title:     { type: String, required: true },
		// Серая строка-пояснение под заголовком (как составлена подборка).
		caption:   { type: String, default: '' },
		books:     { type: Array,  default: () => [] },
		// Куда вести по «Смотреть все»; если не задано — кнопка не показывается.
		viewAllTo: { type: String, default: '' }
	},

	methods: {
		// Прокручиваем на ~80% ширины viewport-а в направлении direction.
		scrollByDir(direction) {
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
	margin-bottom: 56px;

	&__header {
		display: flex;
		align-items: flex-start;
		justify-content: space-between;
		gap: 16px;
		margin-bottom: 22px;
	}

	&__heading {
		min-width: 0;
	}

	&__title {
		font-size: 30px;
		font-weight: 800;
		letter-spacing: 0.2px;
		color: #fff;
		line-height: 1.15;
	}

	&__caption {
		margin-top: 4px;
		font-size: 13px;
		color: rgba(255, 255, 255, 0.55);
	}

	&__more-btn {
		text-transform: none;
		font-weight: 600;
		flex-shrink: 0;
	}

	&__viewport {
		position: relative;
	}

	&__track {
		display: flex;
		gap: 28px;
		overflow-x: auto;
		overflow-y: hidden;
		scroll-behavior: smooth;
		padding-bottom: 8px;
		scrollbar-width: none;

		&::-webkit-scrollbar { display: none; }
	}

	// Увеличенные карточки для главного и страницы рекомендаций.
	&__item {
		flex: 0 0 auto;
		width: 180px;

		@media (min-width: 960px)  { width: 210px; }
		@media (min-width: 1280px) { width: 230px; }
	}

	&__arrow {
		position: absolute;
		top: 34%;
		z-index: 2;
		background: rgba(20, 20, 20, 0.85) !important;
		color: #fff !important;
		opacity: 0;
		transition: opacity 0.25s ease;
		box-shadow: 0 6px 20px rgba(0, 0, 0, 0.5);

		&--left  { left: -18px; }
		&--right { right: -18px; }
	}

	@media (hover: hover) {
		&__viewport:hover &__arrow { opacity: 1; }
	}

	@media (max-width: 600px) {
		margin-bottom: 40px;

		&__title   { font-size: 22px; }
		&__caption { font-size: 12px; }
		&__item    { width: 150px; }
		&__arrow   { display: none; }
	}
}
</style>
