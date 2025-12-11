<template>
    <v-card class="catalog-filters" elevation="0" border>
        <div class="pa-4 font-weight-bold text-h6 d-flex align-center">
            <v-btn
                v-if="currentView !== 'menu'"
                icon="mdi-chevron-left"
                variant="text"
                size="small"
                class="mr-2"
                @click="currentView = 'menu'"
            />
            <span>{{ currentTitle }}</span>
            <v-spacer />
            <v-btn
                v-if="currentView === 'menu' && hasActiveFilters"
                variant="text"
                size="small"
                color="error"
                @click="$emit('reset')"
            >
                Сбросить
            </v-btn>
        </div>
        <v-divider />
        <v-window v-model="currentView" touchless class="flex-grow-1">
            <v-window-item value="menu">
                <v-list bg-color="transparent" class="pa-2">
                    <v-list-item
                        rounded="xl"
                        link
                        @click="currentView = 'genres'"
                        prepend-icon="mdi-shape-outline"
                        append-icon="mdi-chevron-right"
                        title="Жанры"
                    >
                        <!-- Показываем кол-во выбранных жанров -->
                        <template v-slot:subtitle v-if="filters.genres.length">
                            <span class="text-primary">Выбрано: {{ filters.genres.length }}</span>
                        </template>
                    </v-list-item>

                    <!-- Кнопка перехода к Годам -->
                    <v-list-item
                        rounded="xl"
                        link
                        class="mt-1"
                        @click="currentView = 'years'"
                        prepend-icon="mdi-calendar-range"
                        append-icon="mdi-chevron-right"
                        title="Годы выпуска"
                    >
                        <template v-slot:subtitle v-if="filters.yearFrom || filters.yearTo">
                            <span class="text-primary">{{ filters.yearFrom || '...' }} - {{ filters.yearTo || '...' }}</span>
                        </template>
                    </v-list-item>

                    <!-- Кнопка перехода к Авторам -->
                    <v-list-item
                        rounded="xl"
                        link
                        class="mt-1"
                        @click="currentView = 'authors'"
                        prepend-icon="mdi-account-group-outline"
                        append-icon="mdi-chevron-right"
                        title="Авторы"
                    />
                </v-list>
            </v-window-item>

            <!-- ЭКРАН 2: Выбор Жанров -->
            <v-window-item value="genres">
                <div class="pa-2" style="max-height: 300px; overflow-y: auto;">
                    <v-checkbox
                        v-for="genre in genres"
                        :key="genre.id"
                        v-model="filters.genres"
                        :label="genre.name"
                        :value="genre.id"
                        hide-details
                        density="compact"
                        class="mb-1"
                    />
                </div>
            </v-window-item>

            <!-- ЭКРАН 3: Выбор Годов -->
            <v-window-item value="years">
                <div class="pa-4">
                    <div class="text-caption mb-2 text-grey">Укажите диапазон</div>
                    <div class="d-flex gap-2 align-center">
                        <v-text-field
                            v-model="filters.yearFrom"
                            label="С"
                            density="compact"
                            hide-details
                            variant="outlined"
                            type="number"
                        />
                        <span class="mx-2">—</span>
                        <v-text-field
                            v-model="filters.yearTo"
                            label="По"
                            density="compact"
                            hide-details
                            variant="outlined"
                            type="number"
                        />
                    </div>
                </div>
            </v-window-item>

             <!-- ЭКРАН 4: Выбор Авторов -->
             <v-window-item value="authors">
                <div class="pa-4">
                     <v-text-field
                        v-model="authorSearch"
                        placeholder="Поиск автора..."
                        prepend-inner-icon="mdi-magnify"
                        density="compact"
                        variant="outlined"
                        hide-details
                        class="mb-4"
                    />
                    <div class="text-center text-caption text-grey">
                        Введите имя для поиска
                    </div>
                </div>
            </v-window-item>

        </v-window>

        <v-divider />

        <!-- Кнопка Применить (всегда внизу) -->
        <div class="pa-4">
            <v-btn 
                block 
                color="primary" 
                size="large" 
                rounded="xl"
                @click="onApply"
            >
                {{ currentView === 'menu' ? 'Применить фильтры' : 'Готово' }}
            </v-btn>
        </div>
    </v-card>
</template>

<script>
export default {
    name: 'CatalogFilters',
    emits: ['reset', 'apply'],
    data() {
        return {
            currentView: 'menu',
            authorSearch: '',
            filters: {
                genres: [],
                yearFrom: null,
                yearTo: null,
                authors: []
            },
            genres: [
                { id: 'fantasy', name: 'Фэнтези' },
                { id: 'scifi', name: 'Фантастика' },
                { id: 'detective', name: 'Детектив' },
                { id: 'romance', name: 'Романтика' },
                { id: 'horror', name: 'Ужасы' }
            ]
        }
    },
    computed: {
        currentTitle() {
            switch (this.currentView) {
                case 'genres': return 'Выберите жанры'
                case 'years': return 'Период'
                case 'authors': return 'Авторы'
                default: return 'Фильтры'
            }
        },
        hasActiveFilters() {
            return this.filters.genres.length > 0 || this.filters.yearFrom || this.filters.yearTo
        }
    },
    methods: {
        onApply() {
            if (this.currentView !== 'menu') {
                this.currentView = 'menu'
            } else {
                this.$emit('apply', this.filters)
            }
        }
    }
}
</script>

<style scoped>
.catalog-filters {
    position: sticky;
    top: 100px;

    background: rgba(0, 0, 0, 0.7) !important;
    border-radius: 24px !important;
    border: 1px solid rgba(255, 255, 255, 0.1);
    box-shadow: 0 10px 40px rgba(0, 0, 0, 0.4);
    backdrop-filter: saturate(180%) blur(5px);
    overflow: hidden;
}

.v-list-item {
    margin-bottom: 4px;
    transition: background 0.2s;
}

.v-list-item:hover {
    background: rgba(255,255,255, 0.05);
}
</style>
