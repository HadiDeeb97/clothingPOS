using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothingStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReorderLevelOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultReorderLevel",
                table: "Settings",
                type: "int",
                nullable: false,
                defaultValue: 2); // what new items started with before it could be changed

            migrationBuilder.AddColumn<bool>(
                name: "ZeroReorderNeverLow",
                table: "Settings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultReorderLevel",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ZeroReorderNeverLow",
                table: "Settings");
        }
    }
}
