using Harmony.Identity.Domain.Entities.Aggregates.IdentityLinkModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmony.Identity.Infrastructure.EntityConfigurations.CompanyLogin;

/// <summary>
/// The ExternalIdentityLinkPeriods table (ERD cluster five): the link's history. One open period per link (the filtered
/// unique index); a period never ends before it began (the CHECK).
/// </summary>
public class IdentityLinkPeriodConfiguration : IEntityTypeConfiguration<IdentityLinkPeriod>
{
    public void Configure(EntityTypeBuilder<IdentityLinkPeriod> builder)
    {
        builder.ToTable("ExternalIdentityLinkPeriods", table =>
            table.HasCheckConstraint("CK_ExternalIdentityLinkPeriods_Order", "[ToUtc] IS NULL OR [ToUtc] >= [FromUtc]"));

        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.IsOpen);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.LinkId).IsRequired();
        builder.Property(e => e.UserAccountId).IsRequired();
        builder.Property(e => e.FromUtc).IsRequired();

        builder.HasIndex(e => e.LinkId).IsUnique().HasFilter("[ToUtc] IS NULL").HasDatabaseName("UX_ExternalIdentityLinkPeriods_OneOpen");

        builder.HasOne<ExternalIdentityLink>().WithMany(l => l.Periods).HasForeignKey(e => e.LinkId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
