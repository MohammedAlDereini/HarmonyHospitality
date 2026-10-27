using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SignInRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocalStaffEnrolments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SponsorUserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FederatedAuthorityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InviteeHandleDigest = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalStaffEnrolments", x => x.Id);
                    table.CheckConstraint("CK_LocalStaffEnrolments_State", "[State] IN (N'Pending', N'Redeemed', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_LocalStaffEnrolments_FederatedAuthorities_FederatedAuthorityId",
                        column: x => x.FederatedAuthorityId,
                        principalTable: "FederatedAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocalStaffEnrolments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocalStaffEnrolments_UserAccounts_SponsorUserAccountId",
                        column: x => x.SponsorUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocalStaffEnrolments_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperatorSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Methods = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MultiFactorSatisfied = table.Column<bool>(type: "bit", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorSessions", x => x.Id);
                    table.CheckConstraint("CK_OperatorSessions_Order", "[EndedAtUtc] IS NULL OR [EndedAtUtc] >= [StartedAtUtc]");
                    table.ForeignKey(
                        name: "FK_OperatorSessions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatorSessions_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StepUpAuthorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorUserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FederatedAuthorityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepUpAuthorizations", x => x.Id);
                    table.CheckConstraint("CK_StepUpAuthorizations_Method", "[Method] IN (N'password', N'passkey', N'company')");
                    table.ForeignKey(
                        name: "FK_StepUpAuthorizations_FederatedAuthorities_FederatedAuthorityId",
                        column: x => x.FederatedAuthorityId,
                        principalTable: "FederatedAuthorities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StepUpAuthorizations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StepUpAuthorizations_UserAccounts_OperatorUserAccountId",
                        column: x => x.OperatorUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocalStaffEnrolments_CreatedDate",
                table: "LocalStaffEnrolments",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_LocalStaffEnrolments_FederatedAuthorityId",
                table: "LocalStaffEnrolments",
                column: "FederatedAuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalStaffEnrolments_SponsorUserAccountId",
                table: "LocalStaffEnrolments",
                column: "SponsorUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalStaffEnrolments_Tenant_State",
                table: "LocalStaffEnrolments",
                columns: new[] { "TenantId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_LocalStaffEnrolments_TenantId",
                table: "LocalStaffEnrolments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_LocalStaffEnrolments_OnePending",
                table: "LocalStaffEnrolments",
                column: "UserAccountId",
                unique: true,
                filter: "[State] = N'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSessions_Account",
                table: "OperatorSessions",
                columns: new[] { "TenantId", "UserAccountId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSessions_CreatedDate",
                table: "OperatorSessions",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSessions_TenantId",
                table: "OperatorSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSessions_UserAccountId",
                table: "OperatorSessions",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_OperatorSessions_OpenKey",
                table: "OperatorSessions",
                column: "SessionKey",
                unique: true,
                filter: "[EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StepUpAuthorizations_Account_Expiry",
                table: "StepUpAuthorizations",
                columns: new[] { "OperatorUserAccountId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StepUpAuthorizations_CreatedDate",
                table: "StepUpAuthorizations",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_StepUpAuthorizations_FederatedAuthorityId",
                table: "StepUpAuthorizations",
                column: "FederatedAuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_StepUpAuthorizations_TenantId",
                table: "StepUpAuthorizations",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalStaffEnrolments");

            migrationBuilder.DropTable(
                name: "OperatorSessions");

            migrationBuilder.DropTable(
                name: "StepUpAuthorizations");
        }
    }
}
