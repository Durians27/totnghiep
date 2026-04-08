using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VelvySkinWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminResponse",
                table: "SupportTickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                table: "SupportTickets",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminResponse",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "SupportTickets");
        }
    }
}
