using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VelvySkinWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddShipperInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShipperName",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipperPhone",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShipperName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShipperPhone",
                table: "Orders");
        }
    }
}
