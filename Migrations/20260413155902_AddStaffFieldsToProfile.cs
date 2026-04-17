using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VelvySkinWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffFieldsToProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InternalNotes",
                table: "UserProfiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrimaryDuty",
                table: "UserProfiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InternalNotes",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PrimaryDuty",
                table: "UserProfiles");
        }
    }
}
