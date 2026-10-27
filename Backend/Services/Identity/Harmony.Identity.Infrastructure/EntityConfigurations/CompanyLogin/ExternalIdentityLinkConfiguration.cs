using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.IdentityLinkModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.CompanyLogin;

/// <summary>
/// The ExternalIdentityLinks table (ERD cluster five): one row per company identity, unique per company login. The
/// account it points at is a real foreign key; an account is never deleted while a link names it (Restrict).
/// </summary>
public class ExternalIdentityLinkConfiguration : IEntityTypeConfiguration<ExternalIdentityLink>
{
    public void Configure(EntityTypeBuilder<ExternalIdentityLink> builder)
    {
        builder.ToTable("ExternalIdentityLinks");

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);
        builder.Ignore(e => e.IsOpen);

        builder.Property(e => e.UserAccountId).IsRequired();
        builder.Property(e => e.FederatedAuthorityId).IsRequired();
        builder.Property(e => e.ProviderSubject).IsRequired().HasMaxLength(ExternalIdentityLink.SubjectMaxLength);
        builder.Property(e => e.LinkedBy).IsRequired().HasMaxLength(ExternalIdentityLink.LinkedByMaxLength);

        builder.HasIndex(e => new { e.FederatedAuthorityId, e.ProviderSubject }).IsUnique().HasDatabaseName("UX_ExternalIdentityLinks_Authority_Subject");
        builder.HasIndex(e => new { e.TenantId, e.UserAccountId }).HasDatabaseName("IX_ExternalIdentityLinks_Account");

        builder.HasOne<FederatedAuthority>().WithMany().HasForeignKey(e => e.FederatedAuthorityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
