using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class SessionCashCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_UserSessions_CountedClosingCash_NonNegative",
                table: "UserSessions",
                sql: "\"CountedClosingCash\" IS NULL OR \"CountedClosingCash\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserSessions_OpeningCashAmount_NonNegative",
                table: "UserSessions",
                sql: "\"OpeningCashAmount\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserSessions_CountedClosingCash_NonNegative",
                table: "UserSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserSessions_OpeningCashAmount_NonNegative",
                table: "UserSessions");
        }
    }
}
