using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VelvySkinWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddDutyColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Duty",
                table: "UserProfiles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duty",
                table: "UserProfiles");
        }
    }
}
