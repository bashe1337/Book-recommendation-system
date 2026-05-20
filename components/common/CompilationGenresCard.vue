<template>
	<!--
		Карточка подборки по жанру.
		Используется в секции «Подборки жанров для вас» на главной.
		Внутри отображает:
		- веером 3 миниатюры обложек (коллаж),
		- название жанра и краткий подзаголовок,
		- количество книг в подборке.
		Фон карточки берётся из gradient жанра — это позволяет визуально
		различать подборки между собой, не вводя новых дизайн-токенов.
	-->
	<v-card
		class="compilation-genres-card"
		:to="`/catalog?genre=${compilation.id}`"
		:elevation="0"
		:style="{ background: compilation.gradient }"
	>
		<div class="compilation-genres-card__covers">
			<!--
				Раскладываем обложки веером: каждая следующая карточка
				слегка повёрнута и смещена вправо. Индекс используется
				для расчёта transform — поэтому здесь важен порядок массива.
			-->
			<img
				v-for="(src, idx) in compilation.previewCovers"
				:key="idx"
				:src="src"
				:alt="compilation.title"
				class="compilation-genres-card__cover"
				:style="coverStyle(idx)"
				loading="lazy"
			/>
		</div>

		<div class="compilation-genres-card__info">
			<div class="compilation-genres-card__title">{{ compilation.title }}</div>
			<div class="compilation-genres-card__subtitle">{{ compilation.subtitle }}</div>
			<div class="compilation-genres-card__count">
				<v-icon size="14" start>mdi-book-open-page-variant-outline</v-icon>
				{{ compilation.bookCount }} книг
			</div>
		</div>
	</v-card>
</template>

<script>
export default {
	name: 'CompilationGenresCard',

	props: {
		compilation: {
			type: Object,
			required: true
		}
	},

	methods: {
		// Возвращает inline-стиль для каждой обложки коллажа.
		// Центральная обложка (idx === 1) поднимается выше и стоит ровно,
		// крайние — наклоняются в свою сторону. Это даёт эффект «веера».
		coverStyle(idx) {
			const offsets = [
				{ rotate: -10, translateX: -28, translateY: 8, z: 1 },
				{ rotate: 0, translateX: 0, translateY: 0, z: 3 },
				{ rotate: 10, translateX: 28, translateY: 8, z: 1 }
			];
			const o = offsets[idx] || offsets[1];
			return {
				transform: `translate(${o.translateX}px, ${o.translateY}px) rotate(${o.rotate}deg)`,
				zIndex: o.z
			};
		}
	}
}
</script>

<style scoped lang="scss">
.compilation-genres-card {
	position: relative;
	border-radius: 16px;
	padding: 20px;
	min-height: 220px;
	display: flex;
	flex-direction: column;
	justify-content: space-between;
	color: #fff;
	overflow: hidden;
	cursor: pointer;
	transition: transform 0.3s ease, box-shadow 0.3s ease;

	// затемняющая виньетка, чтобы текст всегда читался поверх любого градиента
	&::after {
		content: '';
		position: absolute;
		inset: 0;
		background: linear-gradient(180deg, rgba(0,0,0,0) 40%, rgba(0,0,0,0.45) 100%);
		pointer-events: none;
	}

	&:hover {
		transform: translateY(-4px);
		box-shadow: 0 12px 32px rgba(0, 0, 0, 0.35);
	}

	&__covers {
		position: relative;
		height: 110px;
		display: flex;
		justify-content: center;
		align-items: flex-end;
	}

	&__cover {
		position: absolute;
		bottom: 0;
		width: 70px;
		height: 100px;
		object-fit: cover;
		border-radius: 6px;
		box-shadow: 0 6px 16px rgba(0, 0, 0, 0.4);
		background: #2a2a2a;
		transition: transform 0.4s ease;
	}

	&__info {
		position: relative;
		z-index: 2;
	}

	&__title {
		font-size: 20px;
		font-weight: 700;
		letter-spacing: 0.3px;
		margin-bottom: 4px;
	}

	&__subtitle {
		font-size: 13px;
		opacity: 0.85;
		line-height: 1.35;
		display: -webkit-box;
		-webkit-line-clamp: 2;
		-webkit-box-orient: vertical;
		overflow: hidden;
		margin-bottom: 8px;
	}

	&__count {
		display: inline-flex;
		align-items: center;
		gap: 4px;
		font-size: 12px;
		font-weight: 600;
		background: rgba(0, 0, 0, 0.3);
		padding: 4px 10px;
		border-radius: 999px;
		backdrop-filter: blur(6px);
	}
}
</style>
