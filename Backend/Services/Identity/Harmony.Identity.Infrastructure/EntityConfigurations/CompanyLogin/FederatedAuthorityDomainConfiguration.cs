using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.CompanyLogin;

/// <summary>
/// The FederatedAuthorityDomains table: the e-mail domains a company login covers. Not in the ERD; added because a
/// company login routes by e-mail domain (the Mews way) and Google's issuer is the same for every company, so the domain
/// is what names the customer. A domain belongs to one customer on the whole platform (the unique index).
/// </summary>
public class FederatedAuthorityDomainConfiguration : IEntityTypeConfiguration<FederatedAuthorityDomain>
{
    public void Configure(EntityTypeBuilder<FederatedAuthorityDomain> builder)
    {
        builder.ToTable("FederatedAuthorityDomains");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.FederatedAuthorityId).IsRequired();
        builder.Property(e => e.Domain).IsRequired().HasMaxLength(FederatedAuthorityDomain.DomainMaxLength);

        builder.HasIndex(e => e.Domain).IsUnique().HasDatabaseName("UX_FederatedAuthorityDomains_Domain");

        builder.HasOne<FederatedAuthority>().WithMany(a => a.Domains).HasForeignKey(e => e.FederatedAuthorityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
