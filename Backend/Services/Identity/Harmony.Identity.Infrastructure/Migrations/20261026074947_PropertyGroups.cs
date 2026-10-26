using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PropertyGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PropertyGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TypeCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalIdentifier = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Precedence = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyGroups", x => x.Id);
                    table.UniqueConstraint("AK_PropertyGroups_TenantId_Id_TypeCode", x => new { x.TenantId, x.Id, x.TypeCode });
                    table.ForeignKey(
                        name: "FK_PropertyGroups_PropertyGroupTypes_TypeCode",
                        column: x => x.TypeCode,
                        principalTable: "PropertyGroupTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyGroups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroups_CreatedDate",
                table: "PropertyGroups",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroups_TenantId",
                table: "PropertyGroups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyGroups_TypeCode",
                table: "PropertyGroups",
                column: "TypeCode");

            migrationBuilder.CreateIndex(
                name: "UX_PropertyGroups_TenantId_Code",
                table: "PropertyGroups",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyGroups");
        }
    }
}
