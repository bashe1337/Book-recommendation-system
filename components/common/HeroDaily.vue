<template>
	<!--
		Hero «Рекомендация дня» на главной.
		Слева — крупная обложка, справа — название, автор, рейтинг и описание.
		На мобильном (≤600px) колонки складываются вертикально.
	-->
	<section class="hero-daily">
		<div class="hero-daily__inner">
			<div class="hero-daily__cover-col">
				<v-img
					:src="book.cover"
					:aspect-ratio="2/3"
					cover
					class="hero-daily__cover"
				>
					<template #placeholder>
						<div class="d-flex align-center justify-center fill-height bg-grey-darken-3">
							<v-progress-circular indeterminate color="primary" size="24" />
						</div>
					</template>
				</v-img>
			</div>

			<div class="hero-daily__content">
				<!-- Бейдж-метка раздела -->
				<div class="hero-daily__badge">
					<v-icon size="14" start>mdi-star-four-points</v-icon>
					Рекомендация дня
				</div>

				<h1 class="hero-daily__title">{{ book.title }}</h1>
				<div class="hero-daily__author">{{ book.author }}</div>

				<!-- Агрегированный рейтинг: число + иконка звезды -->
				<div class="hero-daily__rating" v-if="book.rating">
					<v-icon size="20" color="amber">mdi-star</v-icon>
					<span class="hero-daily__rating-value">{{ book.rating.toFixed(1) }}</span>
					<span class="hero-daily__rating-caption">средняя оценка</span>
				</div>

				<!-- Описание ограничено через line-clamp в стилях -->
				<p class="hero-daily__description">{{ book.description }}</p>

				<v-btn
					color="white"
					class="hero-daily__cta"
					elevation="0"
					:to="`/book/${book.id}`"
				>
					Подробнее
					<v-icon end>mdi-arrow-right</v-icon>
				</v-btn>
			</div>
		</div>
	</section>
</template>

<script>
export default {
	name: 'HeroDaily',

	props: {
		book: {
			type: Object,
			required: true
		}
	}
}
</script>

<style scoped lang="scss">
.hero-daily {
	// размытое сияние под блоком; обложку используем как фон через :style было бы
	// инвазивно — оставляем простой градиент, который сочетается с тёмным хэдером.
	position: relative;
	padding: 40px 0 32px;

	&__inner {
		max-width: 1440px;
		margin: 0 auto;
		padding: 0 100px;
		display: grid;
		grid-template-columns: 280px 1fr;
		gap: 40px;
		align-items: center;
	}

	&__cover-col {
		width: 100%;
	}

	&__cover {
		border-radius: 16px;
		overflow: hidden;
		box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5);
		background: #2a2a2a;
	}

	&__content {
		color: #fff;
	}

	&__badge {
		display: inline-flex;
		align-items: center;
		gap: 4px;
		padding: 6px 12px;
		background: rgba(255, 255, 255, 0.1);
		border: 1px solid rgba(255, 255, 255, 0.2);
		border-radius: 999px;
		font-size: 12px;
		font-weight: 600;
		letter-spacing: 0.5px;
		text-transform: uppercase;
		margin-bottom: 16px;
		backdrop-filter: blur(8px);
	}

	&__title {
		font-size: 40px;
		font-weight: 800;
		line-height: 1.15;
		letter-spacing: 0.3px;
		margin-bottom: 8px;
	}

	&__author {
		font-size: 16px;
		color: rgba(255, 255, 255, 0.65);
		margin-bottom: 16px;
	}

	&__rating {
		display: flex;
		align-items: center;
		gap: 6px;
		margin-bottom: 16px;
	}

	&__rating-value {
		font-size: 18px;
		font-weight: 700;
	}

	&__rating-caption {
		font-size: 13px;
		color: rgba(255, 255, 255, 0.5);
		margin-left: 4px;
	}

	// Описание ограничиваем по высоте через line-clamp,
	// чтобы Hero не «дышал» при длинных текстах.
	&__description {
		font-size: 15px;
		line-height: 1.55;
		color: rgba(255, 255, 255, 0.8);
		margin-bottom: 24px;
		display: -webkit-box;
		-webkit-line-clamp: 4;
		-webkit-box-orient: vertical;
		overflow: hidden;
		max-width: 640px;
	}

	&__cta {
		font-weight: 600;
		text-transform: none;
		padding: 0 24px !important;
		height: 44px !important;
		border-radius: 30px;
		color: #242424 !important;
	}

	@media (max-width: 960px) {
		&__inner {
			padding: 0 24px;
			grid-template-columns: 220px 1fr;
			gap: 24px;
		}
		&__title { font-size: 32px; }
	}

	@media (max-width: 600px) {
		padding: 24px 0;

		&__inner {
			grid-template-columns: 1fr;
			padding: 0 16px;
			gap: 20px;
		}

		&__cover-col {
			max-width: 200px;
		}

		&__title { font-size: 26px; }
		&__description { -webkit-line-clamp: 3; }
	}
}
</style>
