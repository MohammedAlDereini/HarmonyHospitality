using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Harmony.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RevocationFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthorizationRevocationSequence",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HighWatermark = table.Column<long>(type: "bigint", nullable: false),
                    MinimumRetainedSequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorizationRevocationSequence", x => x.TenantId);
                    table.CheckConstraint("CK_AuthorizationRevocationSequence_Order", "[MinimumRetainedSequence] >= 1 AND [MinimumRetainedSequence] <= [HighWatermark] + 1");
                    table.ForeignKey(
                        name: "FK_AuthorizationRevocationSequence_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RevocationGapTombstones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThroughSequence = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevocationGapTombstones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevocationGapTombstones_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RevocationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TakenAtSequence = table.Column<long>(type: "bigint", nullable: false),
                    TakenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevocationSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevocationSnapshots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SecurityOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityOperations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SecurityOperations_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RevocationSnapshotItems",
                columns: table => new
                {
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageKey = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecurityVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevocationSnapshotItems", x => new { x.SnapshotId, x.PageKey });
                    table.ForeignKey(
                        name: "FK_RevocationSnapshotItems_RevocationSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "RevocationSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RevocationSnapshotItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuthorizationRevocationFeed",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NewSecurityVersion = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorizationRevocationFeed", x => new { x.TenantId, x.Sequence });
                    table.ForeignKey(
                        name: "FK_AuthorizationRevocationFeed_SecurityOperations_CorrelationId",
                        column: x => x.CorrelationId,
                        principalTable: "SecurityOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthorizationRevocationFeed_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthorizationRevocationFeed_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationRevocationFeed_CorrelationId",
                table: "AuthorizationRevocationFeed",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationRevocationFeed_Tenant_Time",
                table: "AuthorizationRevocationFeed",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationRevocationFeed_TenantId",
                table: "AuthorizationRevocationFeed",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationRevocationFeed_UserAccountId",
                table: "AuthorizationRevocationFeed",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationRevocationSequence_TenantId",
                table: "AuthorizationRevocationSequence",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RevocationGapTombstones_Tenant_Through",
                table: "RevocationGapTombstones",
                columns: new[] { "TenantId", "ThroughSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_RevocationGapTombstones_TenantId",
                table: "RevocationGapTombstones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RevocationSnapshotItems_TenantId",
                table: "RevocationSnapshotItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RevocationSnapshots_Tenant_Sequence",
                table: "RevocationSnapshots",
                columns: new[] { "TenantId", "TakenAtSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_RevocationSnapshots_TenantId",
                table: "RevocationSnapshots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityOperations_Tenant_Time",
                table: "SecurityOperations",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityOperations_TenantId",
                table: "SecurityOperations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityOperations_UserAccountId",
                table: "SecurityOperations",
                column: "UserAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthorizationRevocationFeed");

            migrationBuilder.DropTable(
                name: "AuthorizationRevocationSequence");

            migrationBuilder.DropTable(
                name: "RevocationGapTombstones");

            migrationBuilder.DropTable(
                name: "RevocationSnapshotItems");

            migrationBuilder.DropTable(
                name: "SecurityOperations");

            migrationBuilder.DropTable(
                name: "RevocationSnapshots");
        }
    }
}
