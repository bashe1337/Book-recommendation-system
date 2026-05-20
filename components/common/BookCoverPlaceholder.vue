<template>
	<!--
		SVG-заглушка обложки книги.
		Используется, когда поле cover пустое или изображение не загрузилось.
		Подход аналогичен LiveLib/OpenLibrary: текст-заголовок + автор на
		уникальном фоне, сгенерированном из названия.
	-->
	<svg
		:width="width"
		:height="height"
		:viewBox="`0 0 ${width} ${height}`"
		xmlns="http://www.w3.org/2000/svg"
		preserveAspectRatio="xMidYMid slice"
		role="img"
		:aria-label="`Обложка книги: ${title}`"
		class="book-cover-placeholder"
	>
		<!-- Фон: цвет — детерминирован названием, поэтому одна и та же книга всегда выглядит одинаково. -->
		<rect width="100%" height="100%" :fill="bgColor" rx="4" />

		<!-- Нижняя декоративная полоса, контрастный оттенок того же цвета. -->
		<rect :y="height * 0.78" width="100%" :height="height * 0.22" :fill="accentColor" />

		<!--
			Название книги. foreignObject позволяет применить CSS line-clamp
			к многострочному тексту прямо внутри SVG.
		-->
		<foreignObject x="12" y="16" :width="width - 24" :height="height * 0.6">
			<div
				xmlns="http://www.w3.org/1999/xhtml"
				:style="titleStyle"
			>
				{{ title }}
			</div>
		</foreignObject>

		<!-- Автор: одна строка с многоточием при переполнении. -->
		<foreignObject x="12" :y="height * 0.8" :width="width - 24" :height="height * 0.18">
			<div
				xmlns="http://www.w3.org/1999/xhtml"
				:style="authorStyle"
			>
				{{ author }}
			</div>
		</foreignObject>
	</svg>
</template>

<script>
// Хэш-функция djb2-подобная — стабильно отображает строку в число.
// Используем для детерминированного выбора hue.
const hashString = (s) => {
	let h = 0;
	for (let i = 0; i < s.length; i++) {
		h = s.charCodeAt(i) + ((h << 5) - h);
	}
	return Math.abs(h);
};

const generateColor = (str, sat = 45, light = 35) =>
	`hsl(${hashString(str) % 360}, ${sat}%, ${light}%)`;

const generateAccent = (str) => generateColor(str, 55, 25);

export default {
	name: 'BookCoverPlaceholder',

	props: {
		title:  { type: String, required: true },
		author: { type: String, default: '' },
		width:  { type: Number, default: 200 },
		height: { type: Number, default: 300 }
	},

	computed: {
		bgColor()     { return generateColor(this.title); },
		accentColor() { return generateAccent(this.title); },

		// Размер шрифта зависит от ширины обложки — поэтому компонент
		// одинаково хорошо смотрится в карточке 60×90 и в hero 220×330.
		titleFontSize()  { return Math.max(10, Math.round(this.width * 0.085)); },
		authorFontSize() { return Math.max(8,  Math.round(this.width * 0.065)); },
		maxTitleLines()  { return this.height > 200 ? 4 : 2; },

		titleStyle() {
			return {
				color: '#ffffff',
				fontSize: this.titleFontSize + 'px',
				fontWeight: '700',
				lineHeight: '1.3',
				wordBreak: 'break-word',
				display: '-webkit-box',
				WebkitLineClamp: String(this.maxTitleLines),
				WebkitBoxOrient: 'vertical',
				overflow: 'hidden'
			};
		},

		authorStyle() {
			return {
				color: 'rgba(255,255,255,0.85)',
				fontSize: this.authorFontSize + 'px',
				fontWeight: '400',
				whiteSpace: 'nowrap',
				overflow: 'hidden',
				textOverflow: 'ellipsis'
			};
		}
	}
};
</script>

<style scoped>
.book-cover-placeholder {
	display: block;
	width: 100%;
	height: 100%;
	border-radius: inherit;
}
</style>
