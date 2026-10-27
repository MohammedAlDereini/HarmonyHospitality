using Harmony.Identity.Domain.Entities.Aggregates.MakerCheckerModule;
using Harmony.Identity.Domain.Entities.Aggregates.MergeApprovalModule;
using Harmony.Identity.Domain.Entities.Aggregates.StepUpModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.Approvals;

/// <summary>ERD cluster seven: the approval aggregate (five tables) and the merge record.</summary>
public class MakerCheckerRequestConfiguration : IEntityTypeConfiguration<MakerCheckerRequest>
{
    public void Configure(EntityTypeBuilder<MakerCheckerRequest> builder)
    {
        builder.ToTable("MakerCheckerRequests", table =>
        {
            table.HasCheckConstraint("CK_MakerCheckerRequests_State", "[State] IN (N'Pending', N'Approved', N'Rejected', N'Void')");
            table.HasCheckConstraint("CK_MakerCheckerRequests_CheckerNotMaker", "[DecidedByUserAccountId] IS NULL OR [DecidedByUserAccountId] <> [RequestedByUserAccountId]");
        });
        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);
        builder.Property(e => e.Kind).IsRequired().HasMaxLength(MakerCheckerRequest.KindMaxLength);
        builder.Property(e => e.Payload).IsRequired();
        builder.Property(e => e.State).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(e => new { e.TenantId, e.State }).HasDatabaseName("IX_MakerCheckerRequests_Tenant_State");

        builder.HasMany(e => e.Executions).WithOne().HasForeignKey(e => e.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.ExpectedVersions).WithOne().HasForeignKey(e => e.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.QuarantineReleases).WithOne().HasForeignKey(e => e.MakerCheckerRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.TargetUserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DecidedByUserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StepUpAuthorization>().WithMany().HasForeignKey(e => e.MakerStepUpAuthorizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MakerCheckerExecutionConfiguration : IEntityTypeConfiguration<MakerCheckerExecution>
{
    public void Configure(EntityTypeBuilder<MakerCheckerExecution> builder)
    {
        builder.ToTable("MakerCheckerExecutions");
        builder.HasKey(e => e.Id);
        // One execution per request: a second approval at the same moment fails here as well as on the row version.
        builder.HasIndex(e => e.RequestId).IsUnique().HasDatabaseName("UX_MakerCheckerExecutions_Request");
        builder.HasMany(e => e.Items).WithOne().HasForeignKey(e => e.ExecutionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CheckerUserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MakerCheckerExecutionItemConfiguration : IEntityTypeConfiguration<MakerCheckerExecutionItem>
{
    public void Configure(EntityTypeBuilder<MakerCheckerExecutionItem> builder)
    {
        builder.ToTable("MakerCheckerExecutionItems");
        builder.HasKey(e => e.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MakerCheckerExpectedVersionConfiguration : IEntityTypeConfiguration<MakerCheckerExpectedVersion>
{
    public void Configure(EntityTypeBuilder<MakerCheckerExpectedVersion> builder)
    {
        builder.ToTable("MakerCheckerExpectedVersions");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.RequestId, e.UserAccountId }).IsUnique().HasDatabaseName("UX_MakerCheckerExpectedVersions_Request_Account");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QuarantineReleaseConfiguration : IEntityTypeConfiguration<QuarantineRelease>
{
    public void Configure(EntityTypeBuilder<QuarantineRelease> builder)
    {
        builder.ToTable("QuarantineReleases");
        builder.HasKey(e => e.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MergeApprovalConfiguration : IEntityTypeConfiguration<MergeApproval>
{
    public void Configure(EntityTypeBuilder<MergeApproval> builder)
    {
        builder.ToTable("MergeApprovals", table =>
            table.HasCheckConstraint("CK_MergeApprovals_TwoAccounts", "[ProposedUserAccountId] <> [SupersededUserAccountId]"));
        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);
        // An account is merged away once.
        builder.HasIndex(e => e.SupersededUserAccountId).IsUnique().HasDatabaseName("UX_MergeApprovals_Superseded");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ProposedUserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SupersededUserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MakerCheckerRequest>().WithMany().HasForeignKey(e => e.MakerCheckerRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
