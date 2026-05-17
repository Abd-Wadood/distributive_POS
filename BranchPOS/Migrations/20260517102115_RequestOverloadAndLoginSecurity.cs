using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BranchPOS.Migrations
{
    /// <inheritdoc />
    public partial class RequestOverloadAndLoginSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttemptedUserName",
                table: "AuditLogs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "AuditLogs",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "AuditLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "AuditLogs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Info");

            migrationBuilder.Sql("UPDATE \"AuditLogs\" SET \"EventType\" = \"Action\" WHERE \"EventType\" = '';");
            migrationBuilder.Sql("UPDATE \"AuditLogs\" SET \"Severity\" = 'Info' WHERE \"Severity\" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EventType_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_IpAddress_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "IpAddress", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Severity_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "Severity", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EventType_CreatedAt",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_IpAddress_CreatedAt",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Severity_CreatedAt",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId_CreatedAt",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "AttemptedUserName",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "AuditLogs");
        }
    }
}
