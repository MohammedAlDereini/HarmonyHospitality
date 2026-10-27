using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompanyLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsServicePrincipal",
                table: "UserAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Issuer",
                table: "UserAccounts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "local");

            migrationBuilder.AddColumn<bool>(
                name: "PasswordSignIn",
                table: "UserAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "UserAccounts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            // Every account so far was made here: its subject at the local authority is its own id (as User.Create writes it).
            migrationBuilder.Sql("UPDATE [UserAccounts] SET [Subject] = LOWER(CONVERT(nvarchar(36), [Id])) WHERE [Subject] = N'';");

            migrationBuilder.AddColumn<Guid>(
                name: "SupersededByUserAccountId",
                table: "UserAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMfaEnrolled",
                table: "UserAccounts",
                type: "bit",
                nullable: false,
                computedColumnSql: "[TwoFactorEnabled]",
                stored: true);

            migrationBuilder.CreateTable(
                name: "FederatedAuthorities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scheme = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Issuer = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FederatedAuthorities", x => x.Id);
                    table.CheckConstraint("CK_FederatedAuthorities_LocalIsOn", "[Scheme] <> N'Local' OR ([IsEnabled] = 1 AND [Issuer] = N'local')");
                    table.CheckConstraint("CK_FederatedAuthorities_Scheme", "[Scheme] IN (N'Local', N'Microsoft', N'Google')");
                    table.ForeignKey(
                        name: "FK_FederatedAuthorities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalIdentityLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FederatedAuthorityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderSubject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LinkedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIdentityLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalIdentityLinks_FederatedAuthorities_FederatedAuthorityId",
                        column: x => x.FederatedAuthorityId,
                        principalTable: "FederatedAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalIdentityLinks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalIdentityLinks_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FederatedAuthorityDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FederatedAuthorityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FederatedAuthorityDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FederatedAuthorityDomains_FederatedAuthorities_FederatedAuthorityId",
                        column: x => x.FederatedAuthorityId,
                        principalTable: "FederatedAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FederatedAuthorityDomains_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalIdentityLinkPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ExternalIdentityLinkPeriods", x => x.Id);
                    table.CheckConstraint("CK_ExternalIdentityLinkPeriods_Order", "[ToUtc] IS NULL OR [ToUtc] >= [FromUtc]");
                    table.ForeignKey(
                        name: "FK_ExternalIdentityLinkPeriods_ExternalIdentityLinks_LinkId",
                        column: x => x.LinkId,
                        principalTable: "ExternalIdentityLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalIdentityLinkPeriods_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalIdentityLinkPeriods_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_SupersededByUserAccountId",
                table: "UserAccounts",
                column: "SupersededByUserAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_UserAccounts_Issuer_Subject",
                table: "UserAccounts",
                columns: new[] { "Issuer", "Subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinkPeriods_CreatedDate",
                table: "ExternalIdentityLinkPeriods",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinkPeriods_TenantId",
                table: "ExternalIdentityLinkPeriods",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinkPeriods_UserAccountId",
                table: "ExternalIdentityLinkPeriods",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_ExternalIdentityLinkPeriods_OneOpen",
                table: "ExternalIdentityLinkPeriods",
                column: "LinkId",
                unique: true,
                filter: "[ToUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinks_Account",
                table: "ExternalIdentityLinks",
                columns: new[] { "TenantId", "UserAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinks_CreatedDate",
                table: "ExternalIdentityLinks",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinks_TenantId",
                table: "ExternalIdentityLinks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentityLinks_UserAccountId",
                table: "ExternalIdentityLinks",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_ExternalIdentityLinks_Authority_Subject",
                table: "ExternalIdentityLinks",
                columns: new[] { "FederatedAuthorityId", "ProviderSubject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FederatedAuthorities_CreatedDate",
                table: "FederatedAuthorities",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_FederatedAuthorities_TenantId",
                table: "FederatedAuthorities",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_FederatedAuthorities_MicrosoftIssuer",
                table: "FederatedAuthorities",
                column: "Issuer",
                unique: true,
                filter: "[Scheme] = N'Microsoft'");

            migrationBuilder.CreateIndex(
                name: "UX_FederatedAuthorities_Tenant_Scheme",
                table: "FederatedAuthorities",
                columns: new[] { "TenantId", "Scheme" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FederatedAuthorityDomains_CreatedDate",
                table: "FederatedAuthorityDomains",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_FederatedAuthorityDomains_FederatedAuthorityId",
                table: "FederatedAuthorityDomains",
                column: "FederatedAuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_FederatedAuthorityDomains_TenantId",
                table: "FederatedAuthorityDomains",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_FederatedAuthorityDomains_Domain",
                table: "FederatedAuthorityDomains",
                column: "Domain",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAccounts_UserAccounts_SupersededByUserAccountId",
                table: "UserAccounts",
                column: "SupersededByUserAccountId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Every existing customer (and the platform) gets Harmony's own sign-in, the "local" authority (design 3.1).
            migrationBuilder.Sql(@"
INSERT INTO [FederatedAuthorities] ([Id], [TenantId], [Scheme], [Issuer], [Audience], [IsEnabled], [CreatedDate], [CreatedBy])
SELECT NEWID(), t.[Id], N'Local', N'local', NULL, 1, SYSUTCDATETIME(), N'migration'
FROM [Tenants] t
WHERE NOT EXISTS (SELECT 1 FROM [FederatedAuthorities] a WHERE a.[TenantId] = t.[Id] AND a.[Scheme] = N'Local');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserAccounts_UserAccounts_SupersededByUserAccountId",
                table: "UserAccounts");

            migrationBuilder.DropTable(
                name: "ExternalIdentityLinkPeriods");

            migrationBuilder.DropTable(
                name: "FederatedAuthorityDomains");

            migrationBuilder.DropTable(
                name: "ExternalIdentityLinks");

            migrationBuilder.DropTable(
                name: "FederatedAuthorities");

            migrationBuilder.DropIndex(
                name: "IX_UserAccounts_SupersededByUserAccountId",
                table: "UserAccounts");

            migrationBuilder.DropIndex(
                name: "UX_UserAccounts_Issuer_Subject",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "IsMfaEnrolled",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "IsServicePrincipal",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "Issuer",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "PasswordSignIn",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "SupersededByUserAccountId",
                table: "UserAccounts");
        }
    }
}
