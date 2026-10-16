using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "Permissions",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Tenant");

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalIdentifier = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LegalNameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LegalNameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DefaultCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StatusReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    OnboardedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExportWindowEndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenants_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tenants_Currencies_DefaultCurrency",
                        column: x => x.DefaultCurrency,
                        principalTable: "Currencies",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Code",
                table: "Tenants",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CountryCode",
                table: "Tenants",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CreatedDate",
                table: "Tenants",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_DefaultCurrency",
                table: "Tenants",
                column: "DefaultCurrency");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "Permissions");
        }
    }
}
