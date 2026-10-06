using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothingStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Courier",
                table: "Sales",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliverySettlementId",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeliverySettlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    Courier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Expected = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Received = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceivedLbp = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliverySettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliverySettlements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Courier",
                table: "Sales",
                column: "Courier");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_DeliverySettlementId",
                table: "Sales",
                column: "DeliverySettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliverySettlements_CreatedAt",
                table: "DeliverySettlements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DeliverySettlements_UserId",
                table: "DeliverySettlements",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_DeliverySettlements_DeliverySettlementId",
                table: "Sales",
                column: "DeliverySettlementId",
                principalTable: "DeliverySettlements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_DeliverySettlements_DeliverySettlementId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "DeliverySettlements");

            migrationBuilder.DropIndex(
                name: "IX_Sales_Courier",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_DeliverySettlementId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Courier",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DeliverySettlementId",
                table: "Sales");
        }
    }
}
