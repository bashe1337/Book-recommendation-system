<template>
	<v-app>
		<AppBar />
		<v-main>
			<slot></slot>
		</v-main>
	</v-app>
</template>

<script>
import AppBar from '~/components/header/AppBar';

export default {
	components: {
		AppBar
	}
}
</script>

<style lang="scss">
</style>