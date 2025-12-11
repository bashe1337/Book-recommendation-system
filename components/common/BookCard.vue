<template>
	<v-card
		class="book-card"
		:to="`/catalog`"
		:elevation="0"
	>
		<div class="book-card__cover-wrapper">
			<v-img
				:src="book.cover"
				:aspect-ratio="3/4"
				cover
				class="book-card__image"
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

			<div 
				v-if="book.rating"
				class="book-card__rating"
			>
				<v-icon size="16" color="white" start>mdi-star</v-icon>
				<span>{{ book.rating }}</span>
			</div>
		</div>

		<div class="book-card__info mt-3">
			<div class="book-card__title text-body-2 font-weight-bold">
				{{ book.title }}
			</div>
			<div class="book-card__author text-caption text-grey">
				{{ book.authors.join(', ') }}
			</div>
			<div v-if="book.year" class="book-card__year text-caption text-grey-darken-1">
				{{ book.year }}
			</div>
		</div>
	</v-card>
</template>

<script>
export default {
	name: 'BookCard',
	props: {
		book: {
			type: Object,
			required: true
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
	}

	&__rating {
		position: absolute;
		bottom: 8px;
		right: 8px;
		background: rgba(0, 0, 0, 0.7);
		backdrop-filter: blur(10px);
		color: white;
		padding: 5px 10px;
		border-radius: 12px;
		font-size: 14px;
		font-weight: 700;
		display: flex;
		align-items: center;
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
