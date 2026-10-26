using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovementReasonAndBatchRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "StockMovements",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ReceiptBatches",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ReceiptBatches");
        }
    }
}
