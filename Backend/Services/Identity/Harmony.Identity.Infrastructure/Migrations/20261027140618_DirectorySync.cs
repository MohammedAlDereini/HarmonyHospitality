using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DirectorySync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuarantineReleases");

            migrationBuilder.AddColumn<string>(
                name: "DirectoryExternalId",
                table: "UserAccounts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProvisioningConnectionId",
                table: "UserAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InactivityDays",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.CreateTable(
                name: "ProvisioningConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "char(32)", unicode: false, fixedLength: true, maxLength: 32, nullable: false),
                    SecretHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    SecretIssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisioningConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProvisioningConnections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProvisioningConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropertyGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryGroups", x => x.Id);
                    table.CheckConstraint("CK_DirectoryGroups_OneScope", "[PropertyId] IS NULL OR [PropertyGroupId] IS NULL");
                    table.CheckConstraint("CK_DirectoryGroups_ScopeHasRole", "[RoleId] IS NOT NULL OR ([PropertyId] IS NULL AND [PropertyGroupId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_DirectoryGroups_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectoryGroups_PropertyGroups_PropertyGroupId",
                        column: x => x.PropertyGroupId,
                        principalTable: "PropertyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectoryGroups_ProvisioningConnections_ProvisioningConnectionId",
                        column: x => x.ProvisioningConnectionId,
                        principalTable: "ProvisioningConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectoryGroups_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectoryGroups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DirectoryGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectoryGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DirectoryGroupMembers_DirectoryGroups_DirectoryGroupId",
                        column: x => x.DirectoryGroupId,
                        principalTable: "DirectoryGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectoryGroupMembers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectoryGroupMembers_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_UserAccounts_Directory_ExternalId",
                table: "UserAccounts",
                columns: new[] { "ProvisioningConnectionId", "DirectoryExternalId" },
                unique: true,
                filter: "[ProvisioningConnectionId] IS NOT NULL AND [DirectoryExternalId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tenants_InactivityDays",
                table: "Tenants",
                sql: "[InactivityDays] BETWEEN 1 AND 90");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryGroupMembers_Account",
                table: "DirectoryGroupMembers",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryGroupMembers_TenantId",
                table: "DirectoryGroupMembers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_DirectoryGroupMembers_Group_Account",
                table: "DirectoryGroupMembers",
                columns: new[] { "DirectoryGroupId", "UserAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryGroups_CreatedDate",
                table: "DirectoryGroups",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryGroups_PropertyGroupId",
                table: "DirectoryGroups",
                column: "PropertyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryGroups_PropertyId",
                table: "DirectoryGroups",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryGroups_RoleId",
                table: "DirectoryGroups",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectoryGroups_TenantId",
                table: "DirectoryGroups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_DirectoryGroups_Connection_ExternalId",
                table: "DirectoryGroups",
                columns: new[] { "ProvisioningConnectionId", "ExternalId" },
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_DirectoryGroups_Connection_Name",
                table: "DirectoryGroups",
                columns: new[] { "ProvisioningConnectionId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningConnections_CreatedDate",
                table: "ProvisioningConnections",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "UX_ProvisioningConnections_ClientId",
                table: "ProvisioningConnections",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProvisioningConnections_Tenant",
                table: "ProvisioningConnections",
                column: "TenantId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAccounts_ProvisioningConnections_ProvisioningConnectionId",
                table: "UserAccounts",
                column: "ProvisioningConnectionId",
                principalTable: "ProvisioningConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserAccounts_ProvisioningConnections_ProvisioningConnectionId",
                table: "UserAccounts");

            migrationBuilder.DropTable(
                name: "DirectoryGroupMembers");

            migrationBuilder.DropTable(
                name: "DirectoryGroups");

            migrationBuilder.DropTable(
                name: "ProvisioningConnections");

            migrationBuilder.DropIndex(
                name: "UX_UserAccounts_Directory_ExternalId",
                table: "UserAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tenants_InactivityDays",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DirectoryExternalId",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "ProvisioningConnectionId",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "InactivityDays",
                table: "Tenants");

            migrationBuilder.CreateTable(
                name: "QuarantineReleases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MakerCheckerRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuarantineReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuarantineReleases_MakerCheckerRequests_MakerCheckerRequestId",
                        column: x => x.MakerCheckerRequestId,
                        principalTable: "MakerCheckerRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuarantineReleases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuarantineReleases_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuarantineReleases_MakerCheckerRequestId",
                table: "QuarantineReleases",
                column: "MakerCheckerRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantineReleases_TenantId",
                table: "QuarantineReleases",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QuarantineReleases_UserAccountId",
                table: "QuarantineReleases",
                column: "UserAccountId");
        }
    }
}
