using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantAdministrator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_UserAccounts_Tenant_UserName",
                table: "UserAccounts");

            migrationBuilder.CreateIndex(
                name: "UX_UserAccounts_UserName",
                table: "UserAccounts",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_UserAccounts_UserName",
                table: "UserAccounts");

            migrationBuilder.CreateIndex(
                name: "UX_UserAccounts_Tenant_UserName",
                table: "UserAccounts",
                columns: new[] { "TenantId", "NormalizedUserName" },
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");
        }
    }
}
