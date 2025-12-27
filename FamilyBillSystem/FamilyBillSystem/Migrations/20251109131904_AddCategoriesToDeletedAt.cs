using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyBillSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriesToDeletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "categories",
                type: "timestamp",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "categories");
        }
    }
}
