// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
	compatibilityDate: '2025-12-01',

	devtools: { enabled: true },

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
			sass: {
			// глобальные Sass переменные
			}
		}
		},
		ssr: {
		noExternal: ['vuetify']
		}
	},

	// 	vuetify: {
	// 	vuetifyOptions: {
	// 		theme: {
	// 			// 👇 Вот эта строчка отвечает за тему по умолчанию
	// 			defaultTheme: 'light', 
				
	// 			themes: {
	// 				dark: {
	// 					dark: true,
	// 					colors: {
	// 						background: '#121212',
	// 						surface: '#1E1E1E',
	// 						primary: '#FF6B35',
	// 						// ... твои цвета ...
	// 					}
	// 				},
	// 				light: {
	// 					dark: false,
	// 					colors: {
	// 						// ... цвета светлой темы ...
	// 					}
	// 				}
	// 			}
	// 		}
	// 	}
    // },

	ssr: true
})
