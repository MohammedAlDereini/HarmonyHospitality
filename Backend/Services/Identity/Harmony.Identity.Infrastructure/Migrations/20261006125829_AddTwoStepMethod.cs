using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoStepMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TwoStepMethod",
                table: "UserAccounts",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "AuthenticatorApp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TwoStepMethod",
                table: "UserAccounts");
        }
    }
}
