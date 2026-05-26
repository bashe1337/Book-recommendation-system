<template>
	<div class="home">
		<!--
			========================================================
			ГОСТЕВОЙ ЛЕНДИНГ — на всю высоту экрана, без хэдера.
			AppBar скрывает layouts/default.vue для пути «/» + анонима.
			========================================================
		-->
		<section v-if="!isAuthenticated" class="welcome">
			<div class="welcome__inner">
				<div class="welcome__badge">BRS · LIBRARY</div>
				<h1 class="welcome__title">
					Найдите следующую любимую книгу&nbsp;—
					и&nbsp;ту, что после неё
				</h1>
				<p class="welcome__lead">
					BRS — персональный книжный сервис: тысячи произведений,
					поиск с подсказками, оценки и&nbsp;отзывы. После регистрации
					вы получите рекомендации, подобранные под ваши жанры
					и&nbsp;историю чтения.
				</p>

				<ul class="welcome__features">
					<li>
						<v-icon size="26" color="primary">mdi-star-four-points</v-icon>
						<div>
							<div class="welcome__feature-title">Персональные рекомендации</div>
							<div class="welcome__feature-text">
								Подбираем книги под ваш вкус — на основе оценок,
								жанров и&nbsp;того, что читают похожие на вас читатели
							</div>
						</div>
					</li>
					<li>
						<v-icon size="26" color="primary">mdi-magnify</v-icon>
						<div>
							<div class="welcome__feature-title">Умный поиск</div>
							<div class="welcome__feature-text">
								Понимаем опечатки и&nbsp;подсказываем нужное по&nbsp;первым символам
							</div>
						</div>
					</li>
					<li>
						<v-icon size="26" color="primary">mdi-comment-text-outline</v-icon>
						<div>
							<div class="welcome__feature-title">Отзывы и&nbsp;оценки</div>
							<div class="welcome__feature-text">
								Делитесь впечатлениями — система учится на&nbsp;ваших оценках
								и&nbsp;становится точнее
							</div>
						</div>
					</li>
				</ul>

				<div class="welcome__actions">
					<v-btn
						color="primary"
						size="x-large"
						class="welcome__cta"
						to="/register"
					>
						Создать аккаунт
					</v-btn>
					<v-btn
						variant="outlined"
						size="x-large"
						class="welcome__cta"
						to="/login"
					>
						Войти
					</v-btn>
				</div>
			</div>
		</section>

		<v-container v-if="isAuthenticated" class="home__container">
			<!--
				HERO «Рекомендация дня» — GET /api/recommendations/daily
				Бэк сам выбирает книгу и модель раз в сутки.
			-->
			<HeroDaily
				v-if="heroBook"
				:book="heroBook"
				:algorithm-label="dailyCaption"
			/>

			<!-- 1. Популярное — popularity-based -->
			<BookCarousel
				v-if="popularBooks.length"
				title="Популярное"
				caption="Что сейчас читают чаще всего на BRS"
				:books="popularBooks"
				view-all-to="/catalog?filter=popular"
			/>

			<!-- 2. Подборка по жанрам — GET /api/recommendations/by-preferred-genres -->
			<BookCarousel
				v-if="byGenresBooks.length"
				title="Подборка по жанрам"
				:caption="byGenresCaption"
				:books="byGenresBooks"
				view-all-to="/catalog?filter=by-genres"
			/>

			<!-- 3. Книги от похожих читателей — GET /api/recommendations/similar-readers -->
			<BookCarousel
				v-if="similarReaders.length"
				title="Книги от похожих читателей"
				caption="Что нравится тем, у кого вкус близок к вашему"
				:books="similarReaders"
				view-all-to="/recommendations"
			/>

			<EmptyState
				v-if="!loading && !heroBook && !popularBooks.length && !byGenresBooks.length && !similarReaders.length"
				icon="mdi-book-search-outline"
				text="Пока нечего показать. Оцените несколько книг — и здесь появятся рекомендации"
				action-text="Перейти в каталог"
				action-to="/catalog"
			/>
		</v-container>
	</div>
</template>

<script>
import { storeToRefs } from 'pinia';
import { useUserStore } from '~/stores/user';

import HeroDaily from '~/components/common/HeroDaily.vue';
import BookCarousel from '~/components/common/BookCarousel.vue';
import EmptyState from '~/components/common/EmptyState.vue';
import { mapBookSummary, mapBookList } from '~/utils/bookAdapters.js';

export default {
	name: 'HomePage',

	components: { HeroDaily, BookCarousel, EmptyState },

	setup() {
		const userStore = useUserStore();
		const { isAuthenticated, user } = storeToRefs(userStore);
		const api = useApi();
		return { isAuthenticated, user, api };
	},

	data() {
		return {
			loading: true,
			heroBook: null,
			dailyCaption: 'Подобрано под ваш вкус · обновляется ежедневно',

			popularBooks: [],
			byGenresBooks: [],
			similarReaders: []
		};
	},

	computed: {
		// Список названий жанров для подписи. preferredGenreIds — массив Guid,
		// названий тут нет, поэтому показываем нейтральный текст или
		// сообщение о количестве.
		byGenresCaption() {
			const count = this.user?.preferredGenreIds?.length || 0;
			if (count === 0) return 'Книги из ваших любимых жанров';
			return `Книги из ваших любимых жанров (${count})`;
		}
	},

	watch: {
		isAuthenticated: {
			immediate: false,
			handler(v) {
				if (v) this.loadAuthData();
			}
		}
	},

	async mounted() {
		// Гостю ничего не грузим — он видит welcome-блок.
		if (this.isAuthenticated) {
			await this.loadAuthData();
		} else {
			this.loading = false;
		}
	},

	methods: {
		async loadAuthData() {
			this.loading = true;
			// Все 4 блока загружаются параллельно — каждый со своим
			// fallback на пустой список, чтобы один сбой не валил всю страницу.
			const tasks = [
				this.loadDaily(),
				this.loadPopular(),
				this.loadByGenres(),
				this.loadSimilarReaders()
			];
			await Promise.allSettled(tasks);
			this.loading = false;
		},

		// GET /api/recommendations/daily → DailyRecommendationDto { book, model, date, source }
		async loadDaily() {
			try {
				const res = await this.api.get('/api/recommendations/daily');
				this.heroBook = mapBookSummary(res?.book);
				// «Рекомендация дня» обновляется бэком раз в сутки —
				// в подписи показываем нейтральный текст без деталей модели.
				this.dailyCaption = 'Подобрано под ваш вкус · обновляется ежедневно';
			} catch {
				this.heroBook = null;
			}
		},

		// GET /api/recommendations/popular → RecommendationListDto
		async loadPopular() {
			try {
				const res = await this.api.get('/api/recommendations/popular', { query: { limit: 16 } });
				this.popularBooks = mapBookList(res?.items);
			} catch {
				this.popularBooks = [];
			}
		},

		// GET /api/recommendations/by-preferred-genres
		async loadByGenres() {
			try {
				const res = await this.api.get('/api/recommendations/by-preferred-genres', { query: { limit: 16 } });
				this.byGenresBooks = mapBookList(res?.items);
			} catch {
				this.byGenresBooks = [];
			}
		},

		// GET /api/recommendations/similar-readers
		async loadSimilarReaders() {
			try {
				const res = await this.api.get('/api/recommendations/similar-readers', { query: { limit: 16 } });
				this.similarReaders = mapBookList(res?.items);
			} catch {
				this.similarReaders = [];
			}
		}
	}
};
</script>

<style scoped lang="scss">
.home {
	// padding-top: 10px;
	// padding-bottom: 80px;

	&__container {
		max-width: 1440px;
		margin: 0 auto;
		padding: 0 100px;

		@media (max-width: 960px) { padding: 0 24px; }
		@media (max-width: 600px) { padding: 0 16px; }
	}
}

// ============ Welcome (гость) ============
.welcome {
	min-height: 100vh;
	display: flex;
	align-items: center;
	justify-content: center;
	padding: 64px 24px;
	color: #fff;
	background:
		radial-gradient(ellipse at 20% 20%, rgba(245, 210, 107, 0.10) 0%, rgba(0,0,0,0) 50%),
		radial-gradient(ellipse at 80% 80%, rgba(184, 154, 255, 0.10) 0%, rgba(0,0,0,0) 55%);

	&__inner {
		max-width: 900px;
		text-align: center;
	}

	&__badge {
		display: inline-block;
		font-size: 14px;
		font-weight: 600;
		letter-spacing: 0.45em;
		color: rgba(255, 255, 255, 0.7);
		padding: 8px 18px;
		border: 1px solid rgba(255, 255, 255, 0.22);
		border-radius: 999px;
		margin-bottom: 32px;
	}

	&__title {
		font-size: 64px;
		font-weight: 800;
		line-height: 1.1;
		letter-spacing: 0.2px;
		margin-bottom: 24px;

		@media (max-width: 960px) { font-size: 48px; }
		@media (max-width: 600px) { font-size: 32px; }
	}

	&__lead {
		font-size: 20px;
		line-height: 1.55;
		color: rgba(255, 255, 255, 0.8);
		max-width: 720px;
		margin: 0 auto 48px;

		@media (max-width: 600px) { font-size: 16px; }
	}

	&__features {
		list-style: none;
		padding: 0;
		margin: 0 auto 48px;
		max-width: 640px;
		display: grid;
		gap: 20px;

		li {
			display: flex;
			align-items: flex-start;
			gap: 18px;
			text-align: left;
		}
	}

	&__feature-title {
		font-weight: 700;
		font-size: 19px;
		margin-bottom: 2px;
	}

	&__feature-text {
		font-size: 15px;
		color: rgba(255, 255, 255, 0.65);
		line-height: 1.5;
	}

	&__actions {
		display: flex;
		gap: 16px;
		justify-content: center;
		flex-wrap: wrap;
	}

	&__cta {
		text-transform: none;
		font-weight: 600;
		font-size: 16px !important;
		min-width: 220px;
		height: 56px !important;
		border-radius: 999px;
	}
}
</style>
