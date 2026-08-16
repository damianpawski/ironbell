using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ironbell.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_info",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    name_normalised = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    schema_version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    seeded_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    concurrency_token = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_info", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "app_info",
                columns: new[] { "id", "concurrency_token", "name", "name_normalised", "schema_version", "seeded_at_utc" },
                values: new object[] { 1, 0, "Ironbell", "ironbell", "m0", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "ix_app_info_name_normalised",
                table: "app_info",
                column: "name_normalised",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_info");
        }
    }
}
