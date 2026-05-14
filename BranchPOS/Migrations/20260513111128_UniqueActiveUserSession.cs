using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class UniqueActiveUserSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId",
                table: "UserSessions",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSessions_UserId",
                table: "UserSessions");
        }
    }
}
