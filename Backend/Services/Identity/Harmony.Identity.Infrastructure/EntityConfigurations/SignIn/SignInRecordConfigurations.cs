using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.SessionModule;
using Harmony.Identity.Domain.Entities.Aggregates.StepUpModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.SignIn;

/// <summary>The OperatorSessions table (ERD cluster five): one row per sign-in; one open row per Duende session.</summary>
public class OperatorSessionConfiguration : IEntityTypeConfiguration<OperatorSession>
{
    public void Configure(EntityTypeBuilder<OperatorSession> builder)
    {
        builder.ToTable("OperatorSessions", table =>
            table.HasCheckConstraint("CK_OperatorSessions_Order", "[EndedAtUtc] IS NULL OR [EndedAtUtc] >= [StartedAtUtc]"));

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);
        builder.Ignore(e => e.IsOpen);

        builder.Property(e => e.UserAccountId).IsRequired();
        builder.Property(e => e.SessionKey).IsRequired().HasMaxLength(OperatorSession.SessionKeyMaxLength);
        builder.Property(e => e.Methods).IsRequired().HasMaxLength(OperatorSession.MethodsMaxLength);
        builder.Property(e => e.Provider).IsRequired().HasMaxLength(OperatorSession.ProviderMaxLength);
        builder.Property(e => e.MultiFactorSatisfied).IsRequired();
        builder.Property(e => e.StartedAtUtc).IsRequired();
        builder.Property(e => e.EndReason).HasMaxLength(OperatorSession.EndReasonMaxLength);

        builder.HasIndex(e => e.SessionKey).IsUnique().HasFilter("[EndedAtUtc] IS NULL").HasDatabaseName("UX_OperatorSessions_OpenKey");
        builder.HasIndex(e => new { e.TenantId, e.UserAccountId, e.StartedAtUtc }).HasDatabaseName("IX_OperatorSessions_Account");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>The StepUpAuthorizations table (ERD cluster five): minutes-long proofs, never extended (no update path exists).</summary>
public class StepUpAuthorizationConfiguration : IEntityTypeConfiguration<StepUpAuthorization>
{
    public void Configure(EntityTypeBuilder<StepUpAuthorization> builder)
    {
        builder.ToTable("StepUpAuthorizations", table =>
            table.HasCheckConstraint("CK_StepUpAuthorizations_Method", "[Method] IN (N'password', N'passkey', N'company')"));

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);

        builder.Property(e => e.OperatorUserAccountId).IsRequired();
        builder.Property(e => e.FederatedAuthorityId).IsRequired();
        builder.Property(e => e.Method).IsRequired().HasMaxLength(StepUpAuthorization.MethodMaxLength);
        builder.Property(e => e.ExpiresAtUtc).IsRequired();

        builder.HasIndex(e => new { e.OperatorUserAccountId, e.ExpiresAtUtc }).HasDatabaseName("IX_StepUpAuthorizations_Account_Expiry");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.OperatorUserAccountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FederatedAuthority>().WithMany().HasForeignKey(e => e.FederatedAuthorityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
