using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyBillSystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBillTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Tags",
                table: "bills",
                newName: "Remark");

            migrationBuilder.RenameColumn(
                name: "DefaultTags",
                table: "bill_templates",
                newName: "DefaultRemark");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Remark",
                table: "bills",
                newName: "Tags");

            migrationBuilder.RenameColumn(
                name: "DefaultRemark",
                table: "bill_templates",
                newName: "DefaultTags");
        }
    }
}
