// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
	compatibilityDate: '2025-12-01',

	devtools: { enabled: true },

	// Базовый URL .NET-бэкенда. Можно переопределить переменной
	// NUXT_PUBLIC_API_BASE при сборке/раннере.
	runtimeConfig: {
		public: {
			apiBase: process.env.NUXT_PUBLIC_API_BASE || 'http://localhost:5038'
		}
	},

	modules: [
		'@pinia/nuxt',
		'@invictus.codes/nuxt-vuetify'
	],

	css: [
		'vuetify/styles',
		'@mdi/font/css/materialdesignicons.css',
		'~/assets/scss/common.scss'
	],

	build: {
		transpile: ['vuetify']
	},

	vite: {
		css: {
			preprocessorOptions: {
				sass: {}
			}
		},
		ssr: {
			noExternal: ['vuetify']
		}
	},

	// Единая тёмная тема + светло-жёлтый accent (#F5D26B).
	// Используется во всех v-btn color="primary", v-chip, v-progress и т.д.
	vuetify: {
		vuetifyOptions: {
			theme: {
				defaultTheme: 'dark',
				themes: {
					dark: {
						dark: true,
						colors: {
							background: '#0E0E12',
							surface:    '#16161B',
							primary:    '#F5D26B', // светло-жёлтый — основной accent
							secondary:  '#B89AFF',
							success:    '#7AD49F',
							warning:    '#F5B852',
							error:      '#E57373',
							info:       '#8AB4F8'
						}
					}
				}
			}
		}
	},

	ssr: true
})
