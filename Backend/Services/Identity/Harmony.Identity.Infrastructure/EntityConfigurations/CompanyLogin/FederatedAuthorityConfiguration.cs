using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.CompanyLogin;

/// <summary>
/// The FederatedAuthorities table (ERD cluster five). One authority of each kind per customer; one company signs in to one
/// customer (the filtered unique issuer). The scheme is text, like the account's State, so a row reads plainly.
/// </summary>
public class FederatedAuthorityConfiguration : IEntityTypeConfiguration<FederatedAuthority>
{
    public void Configure(EntityTypeBuilder<FederatedAuthority> builder)
    {
        builder.ToTable("FederatedAuthorities", table =>
        {
            table.HasCheckConstraint("CK_FederatedAuthorities_Scheme", "[Scheme] IN (N'Local', N'Microsoft', N'Google')");
            table.HasCheckConstraint("CK_FederatedAuthorities_LocalIsOn", "[Scheme] <> N'Local' OR ([IsEnabled] = 1 AND [Issuer] = N'local')");
        });

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.DomainEvents);
        builder.Ignore(e => e.IsCompany);

        builder.Property(e => e.Scheme).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Issuer).IsRequired().HasMaxLength(FederatedAuthority.IssuerMaxLength);
        builder.Property(e => e.Audience).HasMaxLength(FederatedAuthority.AudienceMaxLength);
        builder.Property(e => e.IsEnabled).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Scheme }).IsUnique().HasDatabaseName("UX_FederatedAuthorities_Tenant_Scheme");
        builder.HasIndex(e => e.Issuer).IsUnique().HasFilter("[Scheme] = N'Microsoft'").HasDatabaseName("UX_FederatedAuthorities_MicrosoftIssuer");

        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
