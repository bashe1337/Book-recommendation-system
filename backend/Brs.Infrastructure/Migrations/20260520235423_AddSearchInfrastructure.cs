using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brs.Infrastructure.Migrations
{
    /// <summary>
    /// Добавляет инфраструктуру полнотекстового и нечёткого поиска по книгам:
    /// GIN-индекс по search_vector, расширение pg_trgm с триграммным индексом
    /// по title и триггер автоматического обновления search_vector.
    /// </summary>
    public partial class AddSearchInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // причина: GIN-индекс ускоряет поиск по tsvector с порядка O(N) до O(log N)
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_books_search_vector ON books USING GIN(search_vector);");

            // причина: pg_trgm + GIN с gin_trgm_ops даёт нечёткий поиск по подстрокам
            // и обработку опечаток через similarity()
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_books_title_trgm ON books USING GIN (\"Title\" gin_trgm_ops);");

            // причина: триггер избавляет приложение от необходимости вручную
            // пересчитывать search_vector — БД сама поддерживает индекс актуальным.
            // setweight назначает приоритеты: заголовок важнее описания при ранжировании.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION update_book_search_vector() RETURNS trigger AS $$
                BEGIN
                    NEW.search_vector :=
                        setweight(to_tsvector('russian', coalesce(NEW.""Title"", '')), 'A') ||
                        setweight(to_tsvector('russian', coalesce(NEW.""Description"", '')), 'B');
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_book_search_vector
                BEFORE INSERT OR UPDATE ON books
                FOR EACH ROW EXECUTE FUNCTION update_book_search_vector();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // причина: удаляем объекты в обратном порядке создания
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_book_search_vector ON books;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_book_search_vector();");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_books_title_trgm;");
            // причина: pg_trgm не удаляем — расширение может использоваться другими объектами
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_books_search_vector;");
        }
    }
}
