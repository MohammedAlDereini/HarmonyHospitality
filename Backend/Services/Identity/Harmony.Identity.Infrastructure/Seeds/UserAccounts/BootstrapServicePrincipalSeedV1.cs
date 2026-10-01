using Harmony.Core.Abstractions;
using Harmony.Core.BuildingBlocks.Infrastructure.Abstractions;
using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Infrastructure.Persistence;
using Harmony.Identity.Infrastructure.Signing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Infrastructure.Seeds.UserAccounts;

/// <summary>
/// The first service principal. Every endpoint needs a token and every token needs a principal, so a
/// fresh database would lock everyone out. This seed creates one machine account from a secret file kept
/// outside the repository. It creates and never overwrites: rotating, suspending or terminating that
/// principal afterwards is an ordinary API call, and a restart does not undo it.
/// </summary>
internal sealed class BootstrapServicePrincipalSeedV1 : IDbSeed<IdentityDbContext>
{
    public int Version => 1;

    public async Task SeedAsync(IdentityDbContext dbContext, Tenant tenant, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var platformTenantId = configuration["ApplicationSettings:ApplicationKeys:PlatformTenantId"]
            ?? throw new InvalidOperationException("ApplicationSettings:ApplicationKeys:PlatformTenantId is not configured.");

        // The platform's own tenant only. A hotel tenant gets its principals through the API.
        if (!string.Equals(tenant.Id, platformTenantId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var settings = serviceProvider.GetRequiredService<IOptions<TokenIssuerSettings>>().Value.BootstrapPrincipal;
        var tenantId = new Guid(tenant.Id);
        var userName = IdentityText.ServicePrincipalCode(settings.Code);

        var exists = await dbContext.Set<HarmonyUser>()
            .IgnoreQueryFilters()
            .AnyAsync(u => u.TenantId == tenantId && u.UserName == userName, cancellationToken);

        if (exists)
        {
            return;
        }

        // The file holds the secret itself; the row gets its digest. The file is read here and nowhere else.
        var secret = (await File.ReadAllTextAsync(settings.SecretPath!, cancellationToken)).Trim();
        var user = HarmonyUser.CreateServicePrincipal(settings.Code, settings.DisplayName, DateTime.UtcNow, secret);

        // Identity's UserManager, as the API uses it: normalised name, security stamp, our DbContext.
        var result = await serviceProvider.GetRequiredService<UserManager<HarmonyUser>>().CreateAsync(user);
        if (!result.Succeeded)
        {
            var codes = string.Join(", ", result.Errors.Select(e => e.Code));
            throw new InvalidOperationException($"The bootstrap service principal '{userName}' could not be created: {codes}.");
        }

        serviceProvider.GetRequiredService<ILogger<BootstrapServicePrincipalSeedV1>>()
            .LogInformation("Bootstrap service principal {Code} created ({UserId}).", userName, user.Id);
    }
}
