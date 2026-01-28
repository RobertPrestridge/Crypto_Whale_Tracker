using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace InvestIt.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockchainNetworks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChainId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApiUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RateLimitPerSecond = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockchainNetworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiRateLimitTrackings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    TokensAvailable = table.Column<int>(type: "int", nullable: false),
                    LastRefreshTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiRateLimitTrackings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiRateLimitTrackings_BlockchainNetworks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "BlockchainNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonitoredWallets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BlockchainNetworkId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoredWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoredWallets_BlockchainNetworks_BlockchainNetworkId",
                        column: x => x.BlockchainNetworkId,
                        principalTable: "BlockchainNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkId = table.Column<int>(type: "int", nullable: false),
                    LastPollTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsHealthy = table.Column<bool>(type: "bit", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastErrorTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringStatuses_BlockchainNetworks_NetworkId",
                        column: x => x.NetworkId,
                        principalTable: "BlockchainNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TxHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    WalletId = table.Column<int>(type: "int", nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ToAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TokenSymbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TokenAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(38,18)", precision: 38, scale: 18, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BlockNumber = table.Column<int>(type: "int", nullable: false),
                    GasUsed = table.Column<decimal>(type: "decimal(38,18)", precision: 38, scale: 18, nullable: true),
                    GasPrice = table.Column<decimal>(type: "decimal(38,18)", precision: 38, scale: 18, nullable: true),
                    RawData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_MonitoredWallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "MonitoredWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationQueues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationQueues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationQueues_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "BlockchainNetworks",
                columns: new[] { "Id", "ApiKey", "ApiUrl", "ChainId", "CreatedAt", "IsActive", "Name", "RateLimitPerSecond" },
                values: new object[,]
                {
                    { 1, null, "https://api.etherscan.io/api", "1", new DateTime(2026, 1, 25, 0, 0, 0, 0, DateTimeKind.Utc), true, "Ethereum", 5 },
                    { 2, null, "https://api.bscscan.com/api", "56", new DateTime(2026, 1, 25, 0, 0, 0, 0, DateTimeKind.Utc), true, "Binance Smart Chain", 5 },
                    { 3, null, "https://api.polygonscan.com/api", "137", new DateTime(2026, 1, 25, 0, 0, 0, 0, DateTimeKind.Utc), true, "Polygon", 5 },
                    { 4, null, "https://api.solscan.io", "solana-mainnet", new DateTime(2026, 1, 25, 0, 0, 0, 0, DateTimeKind.Utc), true, "Solana", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiRateLimitTrackings_NetworkId",
                table: "ApiRateLimitTrackings",
                column: "NetworkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlockchainNetworks_ChainId",
                table: "BlockchainNetworks",
                column: "ChainId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlockchainNetworks_Name",
                table: "BlockchainNetworks",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoredWallets_Address_BlockchainNetworkId",
                table: "MonitoredWallets",
                columns: new[] { "Address", "BlockchainNetworkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoredWallets_BlockchainNetworkId",
                table: "MonitoredWallets",
                column: "BlockchainNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoredWallets_IsActive",
                table: "MonitoredWallets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringStatuses_IsHealthy",
                table: "MonitoringStatuses",
                column: "IsHealthy");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringStatuses_NetworkId",
                table: "MonitoringStatuses",
                column: "NetworkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueues_Status_CreatedAt",
                table: "NotificationQueues",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationQueues_TransactionId",
                table: "NotificationQueues",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Timestamp",
                table: "Transactions",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TokenSymbol",
                table: "Transactions",
                column: "TokenSymbol");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TxHash",
                table: "Transactions",
                column: "TxHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Type",
                table: "Transactions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_WalletId_Timestamp",
                table: "Transactions",
                columns: new[] { "WalletId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiRateLimitTrackings");

            migrationBuilder.DropTable(
                name: "MonitoringStatuses");

            migrationBuilder.DropTable(
                name: "NotificationQueues");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "MonitoredWallets");

            migrationBuilder.DropTable(
                name: "BlockchainNetworks");
        }
    }
}
