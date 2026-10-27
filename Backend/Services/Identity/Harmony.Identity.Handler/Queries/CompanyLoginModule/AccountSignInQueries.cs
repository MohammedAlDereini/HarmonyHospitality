namespace Harmony.Identity.Handler.Queries.CompanyLoginModule;

using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Models.CompanyLoginModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.CompanyLogin;
using Microsoft.AspNetCore.Identity;

/// <summary>How one person of the caller's customer signs in, for the person page.</summary>
public class GetAccountSignInQuery : BaseQueryRequest<CallResponse<AccountSignInModel>>
{
    public Guid Id { get; set; }

    public class GetAccountSignInQueryValidator : AbstractValidator<GetAccountSignInQuery>
    {
        public GetAccountSignInQueryValidator()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);
        }
    }
}

public class GetAccountSignInQueryHandler : IRequestHandler<GetAccountSignInQuery, CallResponse<AccountSignInModel>>
{
    private readonly UserManager<User> userManager;
    private readonly IFederatedAuthorityRepository authorities;
    private readonly IExternalIdentityLinkRepository links;

    public GetAccountSignInQueryHandler(UserManager<User> userManager, IFederatedAuthorityRepository authorities, IExternalIdentityLinkRepository links)
    {
        this.userManager = userManager;
        this.authorities = authorities;
        this.links = links;
    }

    public async Task<CallResponse<AccountSignInModel>> Handle(GetAccountSignInQuery request, CancellationToken cancellationToken)
    {
        // UserManager walls the read: another customer's account is not found.
        var user = await this.userManager.FindByIdAsync(request.Id.ToString());
        if (user is null)
        {
            return CallResponseBuilder.CreateResponse<AccountSignInModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.UserAccount.NotFound));
        }

        var logins = await this.authorities.OfTenantAsync(user.TenantId, cancellationToken);
        var covering = logins.FirstOrDefault(login => login.Covers(user.Email));
        var schemes = logins.ToDictionary(login => login.Id, login => login.Scheme.ToString());
        var held = await this.links.OfAccountAsync(user.TenantId, user.Id, cancellationToken);

        var model = new AccountSignInModel
        {
            PasswordSignIn = user.PasswordSignIn,
            CoveredBy = covering?.Scheme.ToString(),
            SignsInWith = covering is not null && !user.PasswordSignIn ? "Company" : "Password",
            Links = held.Select(link =>
            {
                var latest = link.Periods.OrderByDescending(p => p.FromUtc).First();
                return new CompanyLinkModel
                {
                    Id = link.Id,
                    Provider = schemes.GetValueOrDefault(link.FederatedAuthorityId, "Company"),
                    LinkedBy = link.LinkedBy,
                    IsOpen = link.IsOpen,
                    Since = latest.FromUtc,
                    Until = latest.ToUtc,
                };
            }).ToList(),
        };

        return CallResponseBuilder.CreateResponse<AccountSignInModel>(eCallResponseStatus.Success).HasData(model);
    }
}

/// <summary>
/// The sign-in page's question for a typed e-mail: does a switched-on company login cover it? Answered by the domain alone,
/// except that a person the administrator kept on a Harmony password stays on the password page. Never answered to a
/// client: the page asks it before the password is checked.
/// </summary>
public class GetCompanyRouteQuery : BaseQueryRequest<CompanyRoute?>
{
    public string? Email { get; set; }
}

/// <summary>Where a company e-mail goes: the authentication scheme and the e-mail's domain (the provider's hint).</summary>
public sealed record CompanyRoute(string AuthenticationScheme, string Domain);

public class GetCompanyRouteQueryHandler : IRequestHandler<GetCompanyRouteQuery, CompanyRoute?>
{
    private readonly IFederatedAuthorityRepository authorities;
    private readonly IUserAccountRepository userAccounts;
    private readonly ILookupNormalizer normalizer;

    public GetCompanyRouteQueryHandler(IFederatedAuthorityRepository authorities, IUserAccountRepository userAccounts, ILookupNormalizer normalizer)
    {
        this.authorities = authorities;
        this.userAccounts = userAccounts;
        this.normalizer = normalizer;
    }

    public async Task<CompanyRoute?> Handle(GetCompanyRouteQuery request, CancellationToken cancellationToken)
    {
        var domain = FederatedAuthority.DomainOf(request.Email);
        if (domain is null || !FederatedAuthority.IsDomainName(domain))
        {
            return null;
        }

        var login = await this.authorities.CoveringAsync(domain, cancellationToken);
        if (login is null)
        {
            return null;
        }

        // The administrator's exception, and a person of another customer who happens to use this domain: the password page.
        var account = (await this.userAccounts.FindByUserNameAcrossTenantsAsync(this.normalizer.NormalizeName(request.Email!.Trim()), cancellationToken)).SingleOrDefault();
        if (account is not null && (account.PasswordSignIn || account.TenantId != login.TenantId))
        {
            return null;
        }

        return new CompanyRoute(CompanyLoginSchemes.Of(login.Scheme), domain);
    }
}

/// <summary>Where a signed-in company person confirms it is them: their company login, and the e-mail to hint. Null otherwise.</summary>
public class GetCompanyConfirmRouteQuery : BaseQueryRequest<CompanyConfirmRoute?>
{
    public Guid UserId { get; set; }
}

public sealed record CompanyConfirmRoute(CompanyRoute Route, string Email);

public class GetCompanyConfirmRouteQueryHandler : IRequestHandler<GetCompanyConfirmRouteQuery, CompanyConfirmRoute?>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly IMediator mediator;

    public GetCompanyConfirmRouteQueryHandler(IUserAccountRepository userAccounts, IMediator mediator)
    {
        this.userAccounts = userAccounts;
        this.mediator = mediator;
    }

    public async Task<CompanyConfirmRoute?> Handle(GetCompanyConfirmRouteQuery request, CancellationToken cancellationToken)
    {
        var user = await this.userAccounts.FindByIdAcrossTenantsAsync(request.UserId, cancellationToken);
        if (user?.Email is null)
        {
            return null;
        }

        // The same rule as the sign-in page: the company covers the e-mail and the person was not kept on a password.
        var route = await this.mediator.Send(new GetCompanyRouteQuery { Email = user.Email }, cancellationToken);
        return route is null ? null : new CompanyConfirmRoute(route, user.Email);
    }
}
