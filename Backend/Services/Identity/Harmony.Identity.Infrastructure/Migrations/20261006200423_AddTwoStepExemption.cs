using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoStepExemption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TwoStepExemptReason",
                table: "UserAccounts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TwoStepExemptReason",
                table: "UserAccounts");
        }
    }
}
