using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyBillSystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserAgin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Users",
                type: "enum('active','frozen')",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "enum('active','inactive','frozen')")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Users",
                type: "enum('active','inactive','frozen')",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "enum('active','frozen')")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
