<template>
	<!--
		Карточка книги.
		Клик по карточке ведёт на детальную страницу книги /book/:id.
		Рейтинг отрисован поверх обложки в правом верхнем углу.
		Принимает как старый формат (authors: string[]), так и новый
		(author: string) — карточка используется в нескольких секциях.
	-->
	<v-card
		class="book-card"
		:to="`/book/${book.id}`"
		:elevation="0"
	>
		<div class="book-card__cover-wrapper">
			<!--
				Если обложка задана и не упала с ошибкой загрузки —
				показываем картинку. Иначе — SVG-заглушку с названием.
			-->
			<v-img
				v-if="book.cover && !coverError"
				:src="book.cover"
				:aspect-ratio="2/3"
				cover
				class="book-card__image"
				@error="coverError = true"
			>
				<template #placeholder>
					<div class="d-flex align-center justify-center fill-height bg-grey-darken-3">
						<v-progress-circular
							indeterminate
							color="primary"
							size="20"
						/>
					</div>
				</template>
			</v-img>
			<div v-else class="book-card__image book-card__image--placeholder">
				<BookCoverPlaceholder
					:title="book.title"
					:author="authorLabel"
					:width="200"
					:height="300"
				/>
			</div>

			<!--
				Бейдж рейтинга поверх обложки в правом верхнем углу.
				Размещаем именно сверху, как требует ТЗ.
			-->
			<div
				v-if="book.rating"
				class="book-card__rating"
			>
				<v-icon size="14" color="amber" start>mdi-star</v-icon>
				<span>{{ formattedRating }}</span>
			</div>
		</div>

		<div class="book-card__info mt-3">
			<div class="book-card__title text-body-2 font-weight-bold">
				{{ book.title }}
			</div>
			<div class="book-card__author text-caption text-grey">
				{{ authorLabel }}
			</div>
			<div v-if="book.year" class="book-card__year text-caption text-grey-darken-1">
				{{ book.year }}
			</div>
		</div>
	</v-card>
</template>

<script>
import BookCoverPlaceholder from '~/components/common/BookCoverPlaceholder.vue';

export default {
	name: 'BookCard',
	components: { BookCoverPlaceholder },
	props: {
		book: {
			type: Object,
			required: true
		}
	},
	data() {
		return {
			// Флаг ошибки загрузки обложки. Сбрасывается при смене URL книги
			// (watch ниже) — иначе при переиспользовании карточки в slider
			// заглушка показывалась бы дольше нужного.
			coverError: false
		}
	},
	watch: {
		'book.cover'() {
			this.coverError = false
		}
	},
	computed: {
		// Унифицируем отображение автора: поддерживаем оба варианта
		// — массив (старые моки/Google Books) и одиночную строку.
		authorLabel() {
			if (Array.isArray(this.book.authors)) {
				return this.book.authors.join(', ');
			}
			return this.book.author || 'Неизвестный автор';
		},
		// Рейтинг приводим к одной десятой — чтобы 4 и 4.9 отображались
		// одинаково (4.0 и 4.9), и бейдж выглядел стабильно.
		formattedRating() {
			const n = Number(this.book.rating);
			return Number.isFinite(n) ? n.toFixed(1) : this.book.rating;
		}
	}
}
</script>

<style scoped lang="scss">
.book-card {
	background: transparent !important;
	cursor: pointer;
	overflow: visible !important;
	transition: all 1s ease-out;
	
	& :deep(.v-card__overlay) {
		background: transparent !important;
		opacity: 0 !important;
	}

	&:hover {
		background: transparent !important;
	}

	&:hover {
		.book-card__image {
			transform: scale(1.1);
			transition: all 0.6s ease-out;
		}
	}

	&__cover-wrapper {
		position: relative;
		border-radius: 12px;
		overflow: hidden;
	}

	&__image {
		border-radius: 12px;
		transition: transform 0.6s ease-out;
		background: #2a2a2a;

		&--placeholder {
			aspect-ratio: 2 / 3;
			overflow: hidden;
		}
	}

	&__rating {
		// Бейдж рейтинга перенесён в правый верхний угол по ТЗ.
		position: absolute;
		top: 8px;
		right: 8px;
		background: rgba(0, 0, 0, 0.65);
		backdrop-filter: blur(10px);
		color: white;
		padding: 4px 9px;
		border-radius: 999px;
		font-size: 12px;
		font-weight: 700;
		display: flex;
		align-items: center;
		gap: 2px;
	}

	&__title {
		line-height: 1.3;
		display: -webkit-box;
		-webkit-box-orient: vertical;
		overflow: hidden;
		margin-bottom: 2px;
		transition: color 0.2s ease;
	}

	&__author {
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
	}
}
</style>
