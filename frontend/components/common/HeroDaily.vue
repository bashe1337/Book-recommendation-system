<template>
    <!--
        Hero «Рекомендация дня» на главной.
        Слева — крупная обложка, справа — название, автор, рейтинг и описание.
        На мобильном (≤600px) колонки складываются вертикально.
    -->
    <section class="hero-daily">
        <div class="hero-daily__inner">
            <div class="hero-daily__cover-col">
                <!-- Эффект фонарика: мягкое свечение позади обложки -->
                <div class="hero-daily__cover-glow" aria-hidden="true"></div>

                <!-- Обёртка нужна чтобы обложка лежала поверх glow через z-index -->
                <div class="hero-daily__cover-wrap">
                    <BookCoverPlaceholder
                        :title="book.title"
                        :author="book.author"
                        :width="200"
                        :height="300"
                    />
                </div>
            </div>


            <div class="hero-daily__content">
                <!-- Бейдж-метка раздела -->
                <div class="hero-daily__badge">
                    <v-icon size="14" start>mdi-star-four-points</v-icon>
                    Рекомендация дня
                </div>


                <h1 class="hero-daily__title">{{ book.title }}</h1>
                <div class="hero-daily__author">{{ book.author }}</div>


                <!--
                    Подпись алгоритма — приходит пропом algorithmLabel.
                    Сейчас фиксируем SVD с обновлением раз в день;
                    на бэке это поле возвращает /api/recommendations/daily.
                -->
                <div v-if="algorithmLabel" class="hero-daily__algo">
                    <v-icon size="14" color="primary" start>mdi-cog-outline</v-icon>
                    {{ algorithmLabel }}
                </div>


                <!-- Агрегированный рейтинг: число + иконка звезды -->
                <div class="hero-daily__rating" v-if="book.rating">
                    <v-icon size="20" color="primary">mdi-star</v-icon>
                    <span class="hero-daily__rating-value">{{ book.rating.toFixed(1) }}</span>
                    <span class="hero-daily__rating-caption">средняя оценка</span>
                </div>


                <!-- Описание ограничено через line-clamp в стилях -->
                <p class="hero-daily__description">{{ book.description }}</p>


                <v-btn
                    color="primary"
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
import BookCoverPlaceholder from './BookCoverPlaceholder.vue';


export default {
    name: 'HeroDaily',


    components: { BookCoverPlaceholder },


    props: {
        book: {
            type: Object,
            required: true
        },
        // Опциональная подпись под автором: каким алгоритмом подобрана книга.
        // На бэке поле придёт в ответе /api/recommendations/daily.
        algorithmLabel: {
            type: String,
            default: ''
        }
    }
}
</script>


<style scoped lang="scss">
.hero-daily {
    // Размытое сияние под блоком; обложку используем как фон через :style было бы
    // инвазивно — оставляем простой градиент, который сочетается с тёмным хэдером.
	position: relative;
	margin-block: 30px;
	padding: 40px 0 32px;
	border: rgb(29, 29, 29) solid 1px;
	border-radius: 6px;
	overflow: hidden;


    &__inner {
        max-width: 1440px;
        margin: 0 auto;
        padding: 0 20px;
        display: grid;
        grid-template-columns: 280px 1fr;
        gap: 40px;
        align-items: center;
    }


    // Колонка с обложкой: relative + isolate создают новый stacking context,
    // внутри которого glow (z-index: 0) гарантированно лежит под wrap (z-index: 1).
    &__cover-col {
        width: 100%;
        position: relative;
        isolation: isolate;
        display: flex;
        justify-content: center;
    }


    // Эффект фонарика — мягкое радиальное свечение строго позади обложки.
    // position: absolute + z-index: -1 внутри isolation: isolate
    // гарантирует, что glow никогда не вылезет поверх обложки или соседних элементов.
    &__cover-glow {
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%);
        width: 220px;
        height: 340px;
        border-radius: 50%;
        z-index: -1;
        pointer-events: none;

        // Многослойный radial-gradient имитирует рассеянный луч фонарика:
        // горячая белёсая точка в центре → тёплый янтарь → мягкое затухание
        background: radial-gradient(
            ellipse 60% 75% at 50% 50%,
            rgba(255, 248, 210, 0.55) 0%,
            rgba(255, 225, 140, 0.35) 25%,
            rgba(255, 195, 80,  0.18) 50%,
            rgba(255, 160, 40,  0.06) 72%,
            transparent 100%
        );

        // blur размывает края пятна — без него свет выглядит резко
        filter: blur(22px);

        // Лёгкая пульсация имитирует неровность фонарного луча
        animation: glow-pulse 4s ease-in-out infinite;
    }


    // Обёртка для обложки — лежит поверх glow внутри isolation context
    &__cover-wrap {
        position: relative;
        z-index: 1;
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
        // background: rgba(255, 255, 255, 0.1);
        border: 1px solid rgb(var(--v-theme-primary));
        border-radius: 999px;
        font-size: 12px;
        font-weight: 600;
        letter-spacing: 0.5px;
        text-transform: uppercase;
        margin-bottom: 16px;
        backdrop-filter: blur(8px);
		color: rgb(var(--v-theme-primary));
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
        margin-bottom: 12px;
    }


    &__algo {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        font-size: 12px;
        font-weight: 600;
        letter-spacing: 0.3px;
        color: rgba(255, 255, 255, 0.65);
        background: rgba(245, 210, 107, 0.10);
        border: 1px solid rgba(245, 210, 107, 0.25);
        padding: 4px 10px;
        border-radius: 999px;
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

        &__cover-glow {
            width: 180px;
            height: 290px;
        }
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


        &__cover-glow {
            width: 160px;
            height: 260px;
        }


        &__title { font-size: 26px; }
        &__description { -webkit-line-clamp: 3; }
    }
}


// Keyframe для пульсации фонарика.
// Немного меняем opacity и scale — органичное мерцание без навязчивости.
@keyframes glow-pulse {
    0%, 100% {
        opacity: 1;
        transform: translate(-50%, -50%) scale(1);
    }

    70% {
        opacity: 0.65;
        transform: translate(-50%, -50%) scale(0.95);
    }

    90% {
        opacity: 0.85;
        transform: translate(-50%, -50%) scale(0.98);
    }
}
</style>
