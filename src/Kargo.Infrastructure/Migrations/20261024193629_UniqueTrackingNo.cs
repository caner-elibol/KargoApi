using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kargo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueTrackingNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_TrackingNo_Covering",
                schema: "dbo",
                table: "Shipments");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TrackingNo_Covering",
                schema: "dbo",
                table: "Shipments",
                column: "TrackingNo",
                unique: true)
                .Annotation("SqlServer:Include", new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_TrackingNo_Covering",
                schema: "dbo",
                table: "Shipments");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TrackingNo_Covering",
                schema: "dbo",
                table: "Shipments",
                column: "TrackingNo")
                .Annotation("SqlServer:Include", new[] { "Status", "CreatedAt" });
        }
    }
}
