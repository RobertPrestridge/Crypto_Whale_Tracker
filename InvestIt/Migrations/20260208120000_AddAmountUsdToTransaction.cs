using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestIt.Migrations
{
    /// <inheritdoc />
    public partial class AddAmountUsdToTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountUsd",
                table: "Transactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AmountUsd",
                table: "Transactions",
                column: "AmountUsd");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_AmountUsd",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AmountUsd",
                table: "Transactions");
        }
    }
}
