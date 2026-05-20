<template>
	<!--
		Универсальный блок «пусто».
		Используется во всех местах профиля и в каталоге: единый вид
		пустого состояния — иконка + текст + опциональная CTA-кнопка.
	-->
	<div class="empty-state">
		<v-icon size="48" color="grey">{{ icon }}</v-icon>
		<div class="empty-state__text">{{ text }}</div>
		<v-btn
			v-if="actionText && actionTo"
			color="primary"
			class="mt-3"
			:to="actionTo"
		>
			{{ actionText }}
		</v-btn>
	</div>
</template>

<script>
export default {
	name: 'EmptyState',

	props: {
		icon: { type: String, default: 'mdi-information-outline' },
		text: { type: String, required: true },
		actionText: { type: String, default: '' },
		actionTo:   { type: String, default: '' }
	}
}
</script>

<style scoped lang="scss">
.empty-state {
	text-align: center;
	padding: 48px 16px;
	color: rgba(255, 255, 255, 0.7);

	&__text {
		font-size: 14px;
		margin-top: 10px;
	}
}
</style>
