namespace Harmony.Identity.Handler.Commands.CompanyLoginModule;

using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Repositories;

/// <summary>What the company login doors share: the provider words, the domain rules, and the fallback administrator rule.</summary>
public static class CompanyLoginRules
{
    /// <summary>The provider a door names, Microsoft or Google (any case); null for anything else, Local included.</summary>
    public static AuthorityScheme? ProviderOf(string? text)
        => Enum.TryParse<AuthorityScheme>(text?.Trim(), ignoreCase: true, out var scheme) && scheme != AuthorityScheme.Local && Enum.IsDefined(scheme)
            ? scheme
            : null;

    public static void Domain<T>(AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, string>> domain)
    {
        validator.RuleFor(domain)
            .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.CompanyLogin.DomainRequired)
            .Must(FederatedAuthority.IsDomainName).WithErrorCode(ModelValidationErrorCodes.Identity.CompanyLogin.DomainInvalidFormat);
    }

    /// <summary>
    /// The way back in: with the customer's company logins as they will be after this change (and one person's choice as
    /// it will be), at least one active administrator still signs in with a Harmony password, either because the
    /// administrator kept them on one or because no switched-on company login covers their e-mail. Nothing switched on:
    /// nothing to hold.
    /// </summary>
    public static async Task<bool> FallbackHoldsAsync(
        IUserAccountRepository userAccounts,
        Guid tenantId,
        IReadOnlyCollection<FederatedAuthority> companyLogins,
        Guid? personId,
        bool? personPasswordSignIn,
        CancellationToken cancellationToken)
    {
        if (!companyLogins.Any(login => login.IsCompany && login.IsEnabled))
        {
            return true;
        }

        var administrators = await userAccounts.ActiveAdministratorsAsync(tenantId, cancellationToken);
        return administrators.Any(admin =>
            (admin.Id == personId ? personPasswordSignIn!.Value : admin.PasswordSignIn)
            || !companyLogins.Any(login => login.Covers(admin.Email)));
    }

    /// <summary>The customer's company logins with this one as changed in memory (the others as stored).</summary>
    public static async Task<IReadOnlyList<FederatedAuthority>> WithChangedAsync(IFederatedAuthorityRepository authorities, FederatedAuthority changed, CancellationToken cancellationToken)
    {
        var all = await authorities.OfTenantAsync(changed.TenantId, cancellationToken);
        return all.Where(login => login.Id != changed.Id).Append(changed).ToList();
    }
}
