using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambev.DeveloperEvaluation.ORM.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SaleDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CustomerExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BranchExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.CheckConstraint("CK_Sales_Cancellation_State", "(\"IsCancelled\" = FALSE AND \"CancelledAt\" IS NULL) OR (\"IsCancelled\" = TRUE AND \"CancelledAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_Sales_Deletion_State", "(\"IsDeleted\" = FALSE AND \"DeletedAt\" IS NULL) OR (\"IsDeleted\" = TRUE AND \"DeletedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_Sales_TotalAmount_NonNegative", "\"TotalAmount\" >= 0");
                    table.CheckConstraint("CK_Sales_Version_Positive", "\"Version\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "SaleItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountRate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleItems", x => x.Id);
                    table.CheckConstraint("CK_SaleItems_Cancellation_State", "(\"IsCancelled\" = FALSE AND \"CancelledAt\" IS NULL) OR (\"IsCancelled\" = TRUE AND \"CancelledAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_SaleItems_DiscountRate_Range", "\"DiscountRate\" >= 0 AND \"DiscountRate\" <= 1");
                    table.CheckConstraint("CK_SaleItems_Monetary_Amounts", "\"GrossAmount\" >= 0 AND \"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"GrossAmount\" AND \"TotalAmount\" = \"GrossAmount\" - \"DiscountAmount\"");
                    table.CheckConstraint("CK_SaleItems_Quantity_Range", "\"Quantity\" BETWEEN 1 AND 20");
                    table.CheckConstraint("CK_SaleItems_UnitPrice_Range", "\"UnitPrice\" > 0 AND \"UnitPrice\" <= 1000000");
                    table.ForeignKey(
                        name: "FK_SaleItems_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_ProductExternalId",
                table: "SaleItems",
                column: "ProductExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_SaleId",
                table: "SaleItems",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "UX_SaleItems_SaleId_ProductExternalId",
                table: "SaleItems",
                columns: new[] { "SaleId", "ProductExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BranchExternalId",
                table: "Sales",
                column: "BranchExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerExternalId",
                table: "Sales",
                column: "CustomerExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Deletion_Cancellation",
                table: "Sales",
                columns: new[] { "IsDeleted", "IsCancelled" });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_SaleDate_Id",
                table: "Sales",
                columns: new[] { "SaleDate", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "UX_Sales_SaleNumber",
                table: "Sales",
                column: "SaleNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleItems");

            migrationBuilder.DropTable(
                name: "Sales");
        }
    }
}
