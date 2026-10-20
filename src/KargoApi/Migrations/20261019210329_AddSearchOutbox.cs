using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KargoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SearchOutbox",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShipmentId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SearchOutbox_Pending",
                schema: "dbo",
                table: "SearchOutbox",
                column: "Id",
                filter: "[ProcessedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchOutbox",
                schema: "dbo");
        }
    }
}
