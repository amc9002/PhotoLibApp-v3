using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAvatar",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Backfill accounts created before Name existed with the local
            // part of their email, so an existing user never has a blank
            // display name - they can still change it themselves in Settings.
            migrationBuilder.Sql(
                "UPDATE Users SET Name = substr(Email, 1, instr(Email, '@') - 1) WHERE Name = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bio",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HasAvatar",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Users");
        }
    }
}
