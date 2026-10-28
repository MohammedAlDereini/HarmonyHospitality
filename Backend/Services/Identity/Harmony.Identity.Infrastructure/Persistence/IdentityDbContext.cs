using Harmony.Core.BuildingBlocks;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Core.Models;
using Harmony.Core.Outbox;
using Harmony.Core.Outbox.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Entities.Aggregates.DirectoryModule;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.IdentityLinkModule;
using Harmony.Identity.Domain.Entities.Aggregates.EnrolmentModule;
using Harmony.Identity.Domain.Entities.Aggregates.SessionModule;
using Harmony.Identity.Domain.Entities.Aggregates.StepUpModule;
using Harmony.Identity.Domain.Entities.Aggregates.LookupModule;
using Harmony.Identity.Domain.Entities.Aggregates.MakerCheckerModule;
using Harmony.Identity.Domain.Entities.Aggregates.MergeApprovalModule;
using Harmony.Identity.Domain.Entities.Aggregates.PermissionModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Entities.Aggregates.GroupMembershipModule;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Aggregates.RevocationModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Harmony.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : DbBaseContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<LookupCategory> LookupCategories { get; set; }
    public DbSet<LookupValue> LookupValues { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<CountrySubdivision> CountrySubdivisions { get; set; }
    public DbSet<City> Cities { get; set; }
    public DbSet<TimeZoneRef> TimeZones { get; set; }
    public DbSet<Language> Languages { get; set; }
    public DbSet<CurrencyRef> Currencies { get; set; }
    public DbSet<PropertyTypeRef> PropertyTypes { get; set; }
    public DbSet<PropertyGroupTypeRef> PropertyGroupTypes { get; set; }
    public DbSet<RegulatoryEnvironment> RegulatoryEnvironments { get; set; }
    public DbSet<Module> Modules { get; set; }

    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantSubscription> TenantSubscriptions { get; set; }
    public DbSet<Property> Properties { get; set; }
    public DbSet<PropertyContact> PropertyContacts { get; set; }
    public DbSet<PropertyGroup> PropertyGroups { get; set; }
    public DbSet<BrandDetail> BrandDetails { get; set; }
    public DbSet<PropertyGroupMember> PropertyGroupMembers { get; set; }
    public DbSet<ConfigSetting> ConfigSettings { get; set; }
    public DbSet<ConfigValue> ConfigValues { get; set; }
    public DbSet<FederatedAuthority> FederatedAuthorities { get; set; }
    public DbSet<FederatedAuthorityDomain> FederatedAuthorityDomains { get; set; }
    public DbSet<ExternalIdentityLink> ExternalIdentityLinks { get; set; }
    public DbSet<IdentityLinkPeriod> ExternalIdentityLinkPeriods { get; set; }
    public DbSet<LocalStaffEnrolment> LocalStaffEnrolments { get; set; }
    public DbSet<OperatorSession> OperatorSessions { get; set; }
    public DbSet<StepUpAuthorization> StepUpAuthorizations { get; set; }
    public DbSet<User> UserAccounts { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<RoleAssignment> RoleAssignments { get; set; }
    public DbSet<AccessGrant> AccessGrants { get; set; }
    public DbSet<UserDevice> UserDevices { get; set; }
    public DbSet<UserPasskey> UserPasskeys { get; set; }
    public DbSet<SecurityAuditEvent> SecurityAuditEvents { get; set; }
    public DbSet<SecurityOperation> SecurityOperations { get; set; }
    public DbSet<RevocationSequence> RevocationSequences { get; set; }
    public DbSet<RevocationEntry> RevocationFeed { get; set; }
    public DbSet<RevocationSnapshot> RevocationSnapshots { get; set; }
    public DbSet<RevocationSnapshotItem> RevocationSnapshotItems { get; set; }
    public DbSet<RevocationGapTombstone> RevocationGapTombstones { get; set; }
    public DbSet<MakerCheckerRequest> MakerCheckerRequests { get; set; }
    public DbSet<MergeApproval> MergeApprovals { get; set; }
    public DbSet<ProvisioningConnection> ProvisioningConnections { get; set; }
    public DbSet<DirectoryGroup> DirectoryGroups { get; set; }
    public DbSet<DirectoryGroupMember> DirectoryGroupMembers { get; set; }

    /// <summary>Stage F: the inbox, the framework's request table under the ERD's name (see InboxRepository).</summary>
    public DbSet<ClientRequest> InboxMessages { get; set; }

    // The domain events already written to the outbox by this context: a save called again never writes one twice.
    private readonly HashSet<IDomainEvent> eventsInOutbox = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// A save that bumps a SecurityVersion also writes the revocation feed (see <see cref="RevocationFeedWriter"/>). Both
    /// go in one transaction; the framework retries database calls, so the whole unit runs inside its retry strategy.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Stage F: the events other services must hear about join this same save (the transactional outbox).
        IdentityOutbox.Stage(this, this.eventsInOutbox);

        if (!RevocationFeedWriter.HasBumps(this))
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        if (this.Database.CurrentTransaction is not null)
        {
            await RevocationFeedWriter.AppendAsync(this, cancellationToken);
            return await base.SaveChangesAsync(cancellationToken);
        }

        List<object> added = [];
        return await this.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            // A retry starts again: the lines of the failed try are taken back out and numbered afresh.
            foreach (var row in added)
            {
                this.Entry(row).State = EntityState.Detached;
            }

            await using var transaction = await this.Database.BeginTransactionAsync(ct);
            added = await RevocationFeedWriter.AppendAsync(this, ct);
            var result = await base.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.ApplySeedingConfiguration();

        // Stage F, the inbox: the framework's request table, as the ERD's InboxMessages.
        var inbox = new DbIdempotencyOptions();
        inbox.ConfigureIdempotency("InboxMessages");
        modelBuilder.ApplyIdempotencyConfiguration(inbox);
        modelBuilder.Entity<ClientRequest>().HasIndex(m => m.Time).HasDatabaseName("IX_InboxMessages_Time");

        // Stage F, the outbox: with messaging on, the framework's outbox table joins this context (its installer owns it).
        if (this.ContextServices?.GetService<IOutboxSettings>() is { } outbox)
        {
            modelBuilder.ApplyTransactionalOutbox(outbox.OutboxSchemaName(this.ContextServices.GetService<AppInfo>()?.Name ?? "Identity"));
        }
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
    }
}