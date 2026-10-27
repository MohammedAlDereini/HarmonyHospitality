using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigSettingsAndValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OwnerService = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SystemDefault = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AppliesToTypes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigSettings", x => x.Key);
                    table.CheckConstraint("CK_ConfigSettings_DataType", "[DataType] IN (N'time', N'int', N'bool', N'decimal', N'string')");
                });

            migrationBuilder.CreateTable(
                name: "ConfigValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigValues", x => x.Id);
                    table.CheckConstraint("CK_ConfigValues_Scope", "[Scope] IN (1, 2, 3)");
                    table.CheckConstraint("CK_ConfigValues_TenantScope", "[Scope] <> 1 OR [ScopeId] = [TenantId]");
                    table.ForeignKey(
                        name: "FK_ConfigValues_ConfigSettings_Key",
                        column: x => x.Key,
                        principalTable: "ConfigSettings",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfigValues_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigSettings_CreatedDate",
                table: "ConfigSettings",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigSettings_OwnerService",
                table: "ConfigSettings",
                column: "OwnerService");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigValues_CreatedDate",
                table: "ConfigValues",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigValues_Key",
                table: "ConfigValues",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigValues_TenantId",
                table: "ConfigValues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigValues_TenantId_Key",
                table: "ConfigValues",
                columns: new[] { "TenantId", "Key" });

            migrationBuilder.CreateIndex(
                name: "UX_ConfigValues_Place_Key",
                table: "ConfigValues",
                columns: new[] { "TenantId", "Scope", "ScopeId", "Key" },
                unique: true);

            // Existing customers: the two settings permissions as rows, and on their Administrator system role (a system role
            // is never edited through the API, and a customer's role gets its permissions only at the customer's birth). The
            // platform tenant's rows come from PermissionSeedV1 version 3; both are idempotent.
            migrationBuilder.Sql("""
                INSERT INTO [Permissions] ([Id], [PermissionValue], [Name], [Description], [EndPoint], [IsActive], [Scope], [TenantId], [CreatedDate], [CreatedBy])
                SELECT NEWID(), p.[Value], p.[Name], p.[Description], p.[EndPoint], 1, N'Tenant', t.[Id], SYSUTCDATETIME(), N'migration:ConfigSettingsAndValues'
                FROM [Tenants] t
                CROSS JOIN (VALUES
                    (15, N'View Settings', N'See the customer''s setting values and why a hotel''s value wins.', N'POST api/ConfigValue/mine; POST api/ConfigSetting/get-all; GET api/ConfigSetting/{key}'),
                    (16, N'Manage Settings', N'Set and clear setting values for the customer, a group or a hotel.', N'PUT api/ConfigValue/Set; PUT api/ConfigValue/Clear')
                ) p ([Value], [Name], [Description], [EndPoint])
                WHERE NOT EXISTS (SELECT 1 FROM [Permissions] x WHERE x.[TenantId] = t.[Id] AND x.[PermissionValue] = p.[Value]);

                INSERT INTO [RolePermissions] ([Id], [RoleId], [PermissionId], [TenantId], [CreatedDate], [CreatedBy])
                SELECT NEWID(), r.[Id], p.[Id], r.[TenantId], SYSUTCDATETIME(), N'migration:ConfigSettingsAndValues'
                FROM [Roles] r
                JOIN [Permissions] p ON p.[TenantId] = r.[TenantId] AND p.[PermissionValue] IN (15, 16)
                WHERE r.[Code] = N'ADMINISTRATOR' AND r.[IsSystemRole] = 1
                  AND NOT EXISTS (SELECT 1 FROM [RolePermissions] x WHERE x.[RoleId] = r.[Id] AND x.[PermissionId] = p.[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The older catalogue does not know 15 and 16: its seed would stop on these rows, so they go first.
            migrationBuilder.Sql("""
                DELETE rp FROM [RolePermissions] rp JOIN [Permissions] p ON p.[Id] = rp.[PermissionId] WHERE p.[PermissionValue] IN (15, 16);
                DELETE FROM [Permissions] WHERE [PermissionValue] IN (15, 16);
                """);

            migrationBuilder.DropTable(
                name: "ConfigValues");

            migrationBuilder.DropTable(
                name: "ConfigSettings");
        }
    }
}
