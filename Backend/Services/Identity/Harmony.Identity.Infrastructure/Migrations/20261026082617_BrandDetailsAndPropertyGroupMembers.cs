using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BrandDetailsAndPropertyGroupMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrandDetails",
                columns: table => new
                {
                    PropertyGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ThemeReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LogoFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    FranchisorName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AgreementStartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AgreementEndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RoyaltyPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandDetails", x => x.PropertyGroupId);
                    table.CheckConstraint("CK_BrandDetails_TypeCode", "[TypeCode] = 'Brand'");
                    table.ForeignKey(
                        name: "FK_BrandDetails_PropertyGroups_TenantId_PropertyGroupId_TypeCode",
                        columns: x => new { x.TenantId, x.PropertyGroupId, x.TypeCode },
                        principalTable: "PropertyGroups",
                        principalColumns: new[] { "TenantId", "Id", "TypeCode" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BrandDetails_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PropertyGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyGroupMembers_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyGroupMembers_PropertyGroupTypes_TypeCode",
                        column: x => x.TypeCode,
                        principalTable: "PropertyGroupTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyGroupMembers_PropertyGroups_TenantId_PropertyGroupId_TypeCode",
                        columns: x => new { x.TenantId, x.PropertyGroupId, x.TypeCode },
                        principalTable: "PropertyGroups",
                        principalColumns: new[] { "TenantId", "Id", "TypeCode" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyGroupMembers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrandDetails_TenantId",
                table: "BrandDetails",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BrandDetails_TenantId_PropertyGroupId_TypeCode",
                table: "BrandDetails",
                columns: new[] { "TenantId", "PropertyGroupId", "TypeCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroupMembers_CreatedDate",
                table: "PropertyGroupMembers",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroupMembers_PropertyId",
                table: "PropertyGroupMembers",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroupMembers_TenantId",
                table: "PropertyGroupMembers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroupMembers_TenantId_PropertyGroupId_TypeCode",
                table: "PropertyGroupMembers",
                columns: new[] { "TenantId", "PropertyGroupId", "TypeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroupMembers_TypeCode",
                table: "PropertyGroupMembers",
                column: "TypeCode");

            migrationBuilder.CreateIndex(
                name: "UX_PropertyGroupMembers_OneBrandPerHotel",
                table: "PropertyGroupMembers",
                columns: new[] { "TenantId", "PropertyId" },
                unique: true,
                filter: "[ToUtc] IS NULL AND [TypeCode] = 'Brand'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrandDetails");

            migrationBuilder.DropTable(
                name: "PropertyGroupMembers");
        }
    }
}
