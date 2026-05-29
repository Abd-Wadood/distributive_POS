using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class PreparedInventoryProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPreparedItem",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PreparationRecipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OutputInventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    OutputQuantityBase = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparationRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreparationRecipes_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreparationRecipes_InventoryItems_OutputInventoryItemId",
                        column: x => x.OutputInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreparationBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    PreparationRecipeId = table.Column<int>(type: "integer", nullable: false),
                    OutputInventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    OutputQuantityBase = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparationBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreparationBatches_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PreparationBatches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreparationBatches_InventoryItems_OutputInventoryItemId",
                        column: x => x.OutputInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreparationBatches_InventoryLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "InventoryLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreparationBatches_PreparationRecipes_PreparationRecipeId",
                        column: x => x.PreparationRecipeId,
                        principalTable: "PreparationRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreparationRecipeIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PreparationRecipeId = table.Column<int>(type: "integer", nullable: false),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    QuantityBase = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    DisplayQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    DisplayUnit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparationRecipeIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreparationRecipeIngredients_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreparationRecipeIngredients_PreparationRecipes_Preparation~",
                        column: x => x.PreparationRecipeId,
                        principalTable: "PreparationRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_BranchId",
                table: "PreparationBatches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_CreatedAt",
                table: "PreparationBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_CreatedByUserId",
                table: "PreparationBatches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_LocationId",
                table: "PreparationBatches",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_OutputInventoryItemId",
                table: "PreparationBatches",
                column: "OutputInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationBatches_PreparationRecipeId",
                table: "PreparationBatches",
                column: "PreparationRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_PreparationRecipeIngredients_InventoryItemId",
                table: "PreparationRecipeIngredients",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "UX_PreparationRecipeIngredients_Recipe_Item",
                table: "PreparationRecipeIngredients",
                columns: new[] { "PreparationRecipeId", "InventoryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreparationRecipes_BranchId_IsActive",
                table: "PreparationRecipes",
                columns: new[] { "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PreparationRecipes_OutputInventoryItemId",
                table: "PreparationRecipes",
                column: "OutputInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "UX_PreparationRecipes_BranchId_Name",
                table: "PreparationRecipes",
                columns: new[] { "BranchId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreparationBatches");

            migrationBuilder.DropTable(
                name: "PreparationRecipeIngredients");

            migrationBuilder.DropTable(
                name: "PreparationRecipes");

            migrationBuilder.DropColumn(
                name: "IsPreparedItem",
                table: "InventoryItems");
        }
    }
}
