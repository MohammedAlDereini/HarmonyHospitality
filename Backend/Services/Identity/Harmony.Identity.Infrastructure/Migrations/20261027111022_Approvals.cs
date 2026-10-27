using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Approvals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ApprovalsRequired",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MakerCheckerRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MakerStepUpAuthorizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetUserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RaisedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedByUserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MakerCheckerRequests", x => x.Id);
                    table.CheckConstraint("CK_MakerCheckerRequests_CheckerNotMaker", "[DecidedByUserAccountId] IS NULL OR [DecidedByUserAccountId] <> [RequestedByUserAccountId]");
                    table.CheckConstraint("CK_MakerCheckerRequests_State", "[State] IN (N'Pending', N'Approved', N'Rejected', N'Void')");
                    table.ForeignKey(
                        name: "FK_MakerCheckerRequests_StepUpAuthorizations_MakerStepUpAuthorizationId",
                        column: x => x.MakerStepUpAuthorizationId,
                        principalTable: "StepUpAuthorizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MakerCheckerRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MakerCheckerRequests_UserAccounts_DecidedByUserAccountId",
                        column: x => x.DecidedByUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MakerCheckerRequests_UserAccounts_RequestedByUserAccountId",
                        column: x => x.RequestedByUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MakerCheckerRequests_UserAccounts_TargetUserAccountId",
                        column: x => x.TargetUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MakerCheckerExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckerUserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MakerCheckerExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MakerCheckerExecutions_MakerCheckerRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "MakerCheckerRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MakerCheckerExecutions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MakerCheckerExecutions_UserAccounts_CheckerUserAccountId",
                        column: x => x.CheckerUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MakerCheckerExpectedVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MakerCheckerExpectedVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MakerCheckerExpectedVersions_MakerCheckerRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "MakerCheckerRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MakerCheckerExpectedVersions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MakerCheckerExpectedVersions_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MergeApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposedUserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupersededUserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MakerCheckerRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MergedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MergeApprovals", x => x.Id);
                    table.CheckConstraint("CK_MergeApprovals_TwoAccounts", "[ProposedUserAccountId] <> [SupersededUserAccountId]");
                    table.ForeignKey(
                        name: "FK_MergeApprovals_MakerCheckerRequests_MakerCheckerRequestId",
                        column: x => x.MakerCheckerRequestId,
                        principalTable: "MakerCheckerRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MergeApprovals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MergeApprovals_UserAccounts_ProposedUserAccountId",
                        column: x => x.ProposedUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MergeApprovals_UserAccounts_SupersededUserAccountId",
                        column: x => x.SupersededUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuarantineReleases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MakerCheckerRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "MakerCheckerExecutionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MakerCheckerExecutionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MakerCheckerExecutionItems_MakerCheckerExecutions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "MakerCheckerExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MakerCheckerExecutionItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MakerCheckerExecutionItems_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerExecutionItems_ExecutionId",
                table: "MakerCheckerExecutionItems",
                column: "ExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerExecutionItems_TenantId",
                table: "MakerCheckerExecutionItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerExecutionItems_UserAccountId",
                table: "MakerCheckerExecutionItems",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerExecutions_CheckerUserAccountId",
                table: "MakerCheckerExecutions",
                column: "CheckerUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerExecutions_TenantId",
                table: "MakerCheckerExecutions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_MakerCheckerExecutions_Request",
                table: "MakerCheckerExecutions",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerExpectedVersions_TenantId",
                table: "MakerCheckerExpectedVersions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerExpectedVersions_UserAccountId",
                table: "MakerCheckerExpectedVersions",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "UX_MakerCheckerExpectedVersions_Request_Account",
                table: "MakerCheckerExpectedVersions",
                columns: new[] { "RequestId", "UserAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerRequests_CreatedDate",
                table: "MakerCheckerRequests",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerRequests_DecidedByUserAccountId",
                table: "MakerCheckerRequests",
                column: "DecidedByUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerRequests_MakerStepUpAuthorizationId",
                table: "MakerCheckerRequests",
                column: "MakerStepUpAuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerRequests_RequestedByUserAccountId",
                table: "MakerCheckerRequests",
                column: "RequestedByUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerRequests_TargetUserAccountId",
                table: "MakerCheckerRequests",
                column: "TargetUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerRequests_Tenant_State",
                table: "MakerCheckerRequests",
                columns: new[] { "TenantId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_MakerCheckerRequests_TenantId",
                table: "MakerCheckerRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MergeApprovals_CreatedDate",
                table: "MergeApprovals",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_MergeApprovals_MakerCheckerRequestId",
                table: "MergeApprovals",
                column: "MakerCheckerRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MergeApprovals_ProposedUserAccountId",
                table: "MergeApprovals",
                column: "ProposedUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MergeApprovals_TenantId",
                table: "MergeApprovals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UX_MergeApprovals_Superseded",
                table: "MergeApprovals",
                column: "SupersededUserAccountId",
                unique: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MakerCheckerExecutionItems");

            migrationBuilder.DropTable(
                name: "MakerCheckerExpectedVersions");

            migrationBuilder.DropTable(
                name: "MergeApprovals");

            migrationBuilder.DropTable(
                name: "QuarantineReleases");

            migrationBuilder.DropTable(
                name: "MakerCheckerExecutions");

            migrationBuilder.DropTable(
                name: "MakerCheckerRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalsRequired",
                table: "Tenants");
        }
    }
}
