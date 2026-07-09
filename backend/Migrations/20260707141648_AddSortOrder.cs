using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Photos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Galleries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Backfill so existing rows keep their current chronological
            // order instead of all landing on SortOrder = 0.
            migrationBuilder.Sql("""
                UPDATE Photos SET SortOrder = (
                    SELECT COUNT(*) FROM Photos AS p2
                    WHERE p2.GalleryId = Photos.GalleryId
                      AND (p2.CreatedAtUtc < Photos.CreatedAtUtc
                           OR (p2.CreatedAtUtc = Photos.CreatedAtUtc AND p2.Id < Photos.Id))
                );
                """);

            migrationBuilder.Sql("""
                UPDATE Galleries SET SortOrder = (
                    SELECT COUNT(*) FROM Galleries AS g2
                    WHERE g2.CreatedAtUtc < Galleries.CreatedAtUtc
                       OR (g2.CreatedAtUtc = Galleries.CreatedAtUtc AND g2.Id < Galleries.Id)
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Galleries");
        }
    }
}
