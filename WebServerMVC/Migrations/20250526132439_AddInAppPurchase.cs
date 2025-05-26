using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebServerMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddInAppPurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InAppPurchases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    Store = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    PurchaseToken = table.Column<string>(type: "text", nullable: false),
                    TransactionId = table.Column<string>(type: "text", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationData = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InAppPurchases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InAppPurchases_ClientId",
                table: "InAppPurchases",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_InAppPurchases_PurchasedAt",
                table: "InAppPurchases",
                column: "PurchasedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InAppPurchases_PurchaseToken",
                table: "InAppPurchases",
                column: "PurchaseToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InAppPurchases_Status",
                table: "InAppPurchases",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InAppPurchases");
        }
    }
}
