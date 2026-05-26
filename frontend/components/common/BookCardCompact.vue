<template>
	<!--
		Компактная горизонтальная карточка книги.
		Используется в боковом блоке «С этим также читают».
		Не использует BookCard, потому что у него вертикальная вёрстка
		и большие отступы — здесь нужно умещать 5–6 карточек подряд
		в узкой колонке.
	-->
	<v-card
		class="book-card-compact"
		:to="`/book/${book.id}`"
		:elevation="0"
	>
		<div class="book-card-compact__cover">
			<v-img
				v-if="book.cover && !coverError"
				:src="book.cover"
				:aspect-ratio="2/3"
				cover
				class="book-card-compact__image"
				@error="coverError = true"
			>
				<template #placeholder>
					<div class="d-flex align-center justify-center fill-height bg-grey-darken-3">
						<v-progress-circular indeterminate color="primary" size="14" />
					</div>
				</template>
			</v-img>
			<BookCoverPlaceholder
				v-else
				:title="book.title"
				:author="authorLabel"
				:width="60"
				:height="90"
			/>
		</div>

		<div class="book-card-compact__info">
			<div class="book-card-compact__title">{{ book.title }}</div>
			<div class="book-card-compact__author">{{ authorLabel }}</div>
			<div v-if="book.rating" class="book-card-compact__rating">
				<v-icon size="14" color="primary">mdi-star</v-icon>
				<span>{{ Number(book.rating).toFixed(1) }}</span>
			</div>
		</div>
	</v-card>
</template>

<script>
import BookCoverPlaceholder from '~/components/common/BookCoverPlaceholder.vue';

export default {
	name: 'BookCardCompact',

	components: { BookCoverPlaceholder },

	props: {
		book: { type: Object, required: true }
	},

	data() {
		return { coverError: false }
	},

	watch: {
		'book.cover'() { this.coverError = false }
	},

	computed: {
		// Принимаем оба формата автора, как в обычной BookCard.
		authorLabel() {
			if (Array.isArray(this.book.authors)) {
				return this.book.authors.join(', ');
			}
			return this.book.author || 'Неизвестный автор';
		}
	}
}
</script>

<style scoped lang="scss">
.book-card-compact {
	display: flex;
	gap: 12px;
	padding: 8px;
	background: transparent !important;
	border-radius: 12px;
	cursor: pointer;
	transition: background 0.2s ease;

	&:hover {
		background: rgba(255, 255, 255, 0.05) !important;
	}

	&__cover {
		flex: 0 0 60px;
		width: 60px;
		height: 90px;
		border-radius: 6px;
		overflow: hidden;
		background: #2a2a2a;
	}

	&__image {
		width: 100%;
		height: 100%;
	}

	&__info {
		display: flex;
		flex-direction: column;
		justify-content: center;
		min-width: 0; // позволяет line-clamp работать в гибком контейнере
	}

	&__title {
		font-size: 14px;
		font-weight: 600;
		line-height: 1.3;
		color: #fff;
		display: -webkit-box;
		-webkit-line-clamp: 2;
		-webkit-box-orient: vertical;
		overflow: hidden;
	}

	&__author {
		font-size: 12px;
		color: rgba(255, 255, 255, 0.55);
		margin-top: 2px;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
	}

	&__rating {
		display: flex;
		align-items: center;
		gap: 3px;
		font-size: 12px;
		font-weight: 700;
		color: rgba(255, 255, 255, 0.85);
		margin-top: 4px;
	}
}
</style>
