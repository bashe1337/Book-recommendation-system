<template>
	<!--
		SVG-обложка книги «BRS · LIBRARY».
		Два редакционных шаблона:
		  - A: тонкая двойная рамка + курсивный серифный заголовок (см. «Дюна»);
		  - B: большая арка-портал + плотный санс-серифный заголовок (см. «Властелин колец»).
		Шаблон и цвет фона выбираются детерминированно по хэшу названия — одна
		книга всегда выглядит одинаково, разные книги получают разные обложки.
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
		<!-- Фон. Сплошной тёмный цвет от хэша. -->
		<rect width="100%" height="100%" :fill="palette.bg" rx="4" />

		<!-- Едва заметный градиент сверху вниз — добавляет «бумажного» объёма. -->
		<defs>
			<linearGradient :id="gradId" x1="0" y1="0" x2="0" y2="1">
				<stop offset="0%"   stop-color="rgba(255,255,255,0.05)" />
				<stop offset="100%" stop-color="rgba(0,0,0,0.25)" />
			</linearGradient>
		</defs>
		<rect width="100%" height="100%" :fill="`url(#${gradId})`" rx="4" />

		<!-- Декорация по шаблону. -->
		<g v-if="template === 'frame'" :stroke="palette.accent" stroke-width="1" fill="none">
			<!-- Внешняя рамка -->
			<rect
				:x="frame.outer.x" :y="frame.outer.y"
				:width="frame.outer.w" :height="frame.outer.h"
				:rx="6"
			/>
			<!-- Внутренняя рамка — параллельная, чуть уже -->
			<rect
				:x="frame.inner.x" :y="frame.inner.y"
				:width="frame.inner.w" :height="frame.inner.h"
				:rx="4"
			/>
		</g>

		<g v-else-if="template === 'arch'" :stroke="palette.accent" stroke-width="1.2" fill="none">
			<!-- Большая арка по центру: прямоугольник со скруглённой верхушкой. -->
			<path :d="archPath" />
		</g>

		<!-- BRS · LIBRARY — шапка обложки. -->
		<foreignObject
			:x="width * 0.1"
			:y="height * 0.06"
			:width="width * 0.8"
			:height="height * 0.08"
		>
			<div xmlns="http://www.w3.org/1999/xhtml" :style="brandStyle">
				BRS · LIBRARY
			</div>
		</foreignObject>

		<!-- Название книги. Курсивная serif для frame, плотный sans для arch. -->
		<foreignObject
			:x="width * 0.1"
			:y="height * 0.62"
			:width="width * 0.8"
			:height="height * 0.22"
		>
			<div xmlns="http://www.w3.org/1999/xhtml" :style="titleStyle">
				{{ title }}
			</div>
		</foreignObject>

		<!-- Тонкая разделительная линия между названием и автором. -->
		<line
			:x1="width * 0.35"
			:y1="height * 0.87"
			:x2="width * 0.65"
			:y2="height * 0.87"
			:stroke="palette.accent"
			stroke-width="0.8"
		/>

		<!-- Автор — мелкие капс-литеры с разрядкой. -->
		<foreignObject
			:x="width * 0.1"
			:y="height * 0.88"
			:width="width * 0.8"
			:height="height * 0.1"
		>
			<div xmlns="http://www.w3.org/1999/xhtml" :style="authorStyle">
				{{ (author || '').toUpperCase() }}
			</div>
		</foreignObject>
	</svg>
</template>

<script>
// Простой стабильный хэш строки — нужен, чтобы шаблон и цвет
// детерминированно зависели от названия книги.
const hashString = (s) => {
	let h = 0;
	for (let i = 0; i < (s || '').length; i++) {
		h = s.charCodeAt(i) + ((h << 5) - h);
	}
	return Math.abs(h);
};

// Тёмная палитра, подобранная под референсные скриншоты (Дюна, ВК).
// Каждая запись = {bg, accent} — фон обложки и цвет для рамок/текста.
const PALETTES = [
	{ bg: '#2a1b3d', accent: '#d3b3d8' }, // глубокий фиолетовый (как Дюна)
	{ bg: '#0f2a26', accent: '#cfe4d4' }, // тёмный изумруд (как ВК)
	{ bg: '#1c2a3a', accent: '#cfd9e4' }, // полночный синий
	{ bg: '#2a1414', accent: '#e4cccc' }, // тёмно-винный
	{ bg: '#1f1f1f', accent: '#d8d8d8' }, // графит
	{ bg: '#2a2414', accent: '#e4dbb6' }, // охра/коричневый
	{ bg: '#14252a', accent: '#bcd9d9' }, // тёмная морская волна
	{ bg: '#2a1a0e', accent: '#dcc1a6' }  // тёмный кофе
];

const TEMPLATES = ['frame', 'arch'];

export default {
	name: 'BookCoverPlaceholder',

	props: {
		title:  { type: String, required: true },
		author: { type: String, default: '' },
		width:  { type: Number, default: 200 },
		height: { type: Number, default: 300 }
	},

	computed: {
		hash() { return hashString(this.title); },

		palette() {
			return PALETTES[this.hash % PALETTES.length];
		},

		template() {
			// Шаблон выбираем по второму разряду хэша — независимо от палитры.
			return TEMPLATES[Math.floor(this.hash / 13) % TEMPLATES.length];
		},

		// Уникальный id градиента — обязательно свой на каждую обложку,
		// иначе при нескольких заглушках на странице они начнут шарить
		// один и тот же defs.
		gradId() { return `bcp-grad-${this.hash}`; },

		// Геометрия двойной рамки (шаблон 'frame').
		frame() {
			const o = Math.round(this.width * 0.06);   // отступ внешней рамки от края
			const gap = Math.round(this.width * 0.018); // зазор между рамками
			return {
				outer: { x: o,        y: o,        w: this.width - o * 2,            h: this.height - o * 2 },
				inner: { x: o + gap,  y: o + gap,  w: this.width - (o + gap) * 2,    h: this.height - (o + gap) * 2 }
			};
		},

		// Path-арки (шаблон 'arch').
		// Прямоугольник со скруглённой верхней частью полукругом.
		archPath() {
			const W = this.width;
			const H = this.height;
			const left   = W * 0.18;
			const right  = W * 0.82;
			const bottom = H * 0.94;
			const archTop = H * 0.18;
			const radius = (right - left) / 2;
			// Снизу-слева → вверх → дуга → вниз → закрыть.
			return [
				`M ${left} ${bottom}`,
				`L ${left} ${archTop + radius}`,
				`A ${radius} ${radius} 0 0 1 ${right} ${archTop + radius}`,
				`L ${right} ${bottom}`
			].join(' ');
		},

		brandStyle() {
			return {
				color: this.palette.accent,
				opacity: 0.85,
				fontFamily: '"Helvetica Neue", Arial, sans-serif',
				fontSize: Math.max(8, Math.round(this.width * 0.055)) + 'px',
				fontWeight: '500',
				letterSpacing: '0.25em',
				textAlign: 'center',
				textTransform: 'uppercase'
			};
		},

		titleStyle() {
			const isFrame = this.template === 'frame';
			return {
				color: '#ffffff',
				textAlign: 'center',
				lineHeight: '1.2',
				// Курсивный serif для шаблона рамки (как «Дюна»),
				// плотный sans для арки (как «Властелин колец»).
				fontFamily: isFrame
					? 'Georgia, "Times New Roman", serif'
					: '"Helvetica Neue", Arial, sans-serif',
				fontStyle: isFrame ? 'italic' : 'normal',
				fontWeight: isFrame ? '500' : '800',
				fontSize: this.computedTitleFontSize + 'px',
				display: '-webkit-box',
				WebkitLineClamp: '3',
				WebkitBoxOrient: 'vertical',
				overflow: 'hidden',
				wordBreak: 'break-word'
			};
		},

		// Размер шрифта названия зависит от ширины обложки и длины строки —
		// чтобы длинные русские названия не выпрыгивали из коробки.
		computedTitleFontSize() {
			const base = this.width * 0.115;
			const len = (this.title || '').length;
			// Длиннее ~18 символов — пропорционально уменьшаем кегль.
			const factor = len > 18 ? 18 / len : 1;
			return Math.max(11, Math.round(base * Math.max(0.7, factor)));
		},

		authorStyle() {
			return {
				color: this.palette.accent,
				opacity: 0.85,
				fontFamily: '"Helvetica Neue", Arial, sans-serif',
				fontSize: Math.max(7, Math.round(this.width * 0.045)) + 'px',
				fontWeight: '600',
				letterSpacing: '0.2em',
				textAlign: 'center',
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
