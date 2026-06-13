using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KlawQ.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Product_Type",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Original");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Product_Type",
                table: "Products");
        }
    }
}
