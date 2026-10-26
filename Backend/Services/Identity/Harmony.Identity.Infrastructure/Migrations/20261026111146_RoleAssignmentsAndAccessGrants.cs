using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoleAssignmentsAndAccessGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertyGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GrantedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessGrants", x => x.Id);
                    table.CheckConstraint("CK_AccessGrants_OneScope", "([PropertyId] IS NULL AND [PropertyGroupId] IS NOT NULL) OR ([PropertyId] IS NOT NULL AND [PropertyGroupId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_AccessGrants_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessGrants_PropertyGroups_PropertyGroupId",
                        column: x => x.PropertyGroupId,
                        principalTable: "PropertyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessGrants_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessGrants_UserAccounts_GrantedById",
                        column: x => x.GrantedById,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessGrants_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertyGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAssignments", x => x.Id);
                    table.CheckConstraint("CK_RoleAssignments_OneScope", "([PropertyId] IS NULL AND [PropertyGroupId] IS NOT NULL) OR ([PropertyId] IS NOT NULL AND [PropertyGroupId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_RoleAssignments_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleAssignments_PropertyGroups_PropertyGroupId",
                        column: x => x.PropertyGroupId,
                        principalTable: "PropertyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleAssignments_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleAssignments_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_Account_Window",
                table: "AccessGrants",
                columns: new[] { "TenantId", "UserId", "ToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_CreatedDate",
                table: "AccessGrants",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_GrantedById",
                table: "AccessGrants",
                column: "GrantedById");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_PropertyGroupId",
                table: "AccessGrants",
                column: "PropertyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_PropertyId",
                table: "AccessGrants",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_RoleId",
                table: "AccessGrants",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_TenantId",
                table: "AccessGrants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_UserId",
                table: "AccessGrants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_Account_Open",
                table: "RoleAssignments",
                columns: new[] { "TenantId", "UserId", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_CreatedDate",
                table: "RoleAssignments",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_PropertyGroupId",
                table: "RoleAssignments",
                column: "PropertyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_PropertyId",
                table: "RoleAssignments",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_RoleId",
                table: "RoleAssignments",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_TenantId",
                table: "RoleAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_UserId",
                table: "RoleAssignments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessGrants");

            migrationBuilder.DropTable(
                name: "RoleAssignments");
        }
    }
}
