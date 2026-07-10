using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class ChangeGalleryOwnerIdToGuid : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// EF Core's automatic diff produced an empty migration here, since
        /// SQLite stores both <c>string</c> and <c>Guid</c> columns as
        /// <c>TEXT</c> - it saw no schema-level difference. Written by hand
        /// as a drop-and-readd instead: every existing row's OwnerId is
        /// already <c>null</c> (unused field, confirmed before running this
        /// migration), so there's nothing to convert and no data loss risk.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Galleries_OwnerId",
                table: "Galleries");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Galleries");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Galleries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Galleries_OwnerId",
                table: "Galleries",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Galleries_OwnerId",
                table: "Galleries");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Galleries");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Galleries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Galleries_OwnerId",
                table: "Galleries",
                column: "OwnerId");
        }
    }
}
