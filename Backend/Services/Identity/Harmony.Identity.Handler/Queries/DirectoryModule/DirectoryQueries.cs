namespace Harmony.Identity.Handler.Queries.DirectoryModule;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.DirectoryModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Handler.Commands.DirectoryModule;
using Harmony.Identity.Infrastructure.Signing;
using Microsoft.Extensions.Options;

/// <summary>The caller's own customer's directory connection and groups; null data when it has none.</summary>
public class GetMyDirectoryQuery : BaseQueryRequest<CallResponse<DirectoryModel?>>
{
}

/// <summary>A customer's directory connection, for the platform's console.</summary>
public class GetDirectoryOfTenantQuery : BaseQueryRequest<CallResponse<DirectoryModel?>>
{
    public Guid TenantId { get; set; }

    public class GetDirectoryOfTenantQueryValidator : AbstractValidator<GetDirectoryOfTenantQuery>
    {
        public GetDirectoryOfTenantQueryValidator()
        {
            this.RuleFor(e => e.TenantId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);
        }
    }
}

public class DirectoryQueryHandlers :
    IRequestHandler<GetMyDirectoryQuery, CallResponse<DirectoryModel?>>,
    IRequestHandler<GetDirectoryOfTenantQuery, CallResponse<DirectoryModel?>>
{
    private readonly IProvisioningConnectionRepository connections;
    private readonly IDirectoryGroupRepository groups;
    private readonly IUserAccountRepository userAccounts;
    private readonly ICallContext callContext;
    private readonly string issuer;

    public DirectoryQueryHandlers(IProvisioningConnectionRepository connections, IDirectoryGroupRepository groups, IUserAccountRepository userAccounts, ICallContext callContext, IOptions<TokenIssuerSettings> issuer)
    {
        this.connections = connections;
        this.groups = groups;
        this.userAccounts = userAccounts;
        this.callContext = callContext;
        this.issuer = issuer.Value.Issuer.TrimEnd('/');
    }

    public Task<CallResponse<DirectoryModel?>> Handle(GetMyDirectoryQuery request, CancellationToken cancellationToken)
        => this.OfAsync(Guid.Parse(this.callContext.TenantId()), cancellationToken);

    public Task<CallResponse<DirectoryModel?>> Handle(GetDirectoryOfTenantQuery request, CancellationToken cancellationToken)
        => this.OfAsync(request.TenantId, cancellationToken);

    private async Task<CallResponse<DirectoryModel?>> OfAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var connection = await this.connections.OfTenantAsync(tenantId, cancellationToken);
        if (connection is null)
        {
            return CallResponseBuilder.CreateResponse<DirectoryModel?>(eCallResponseStatus.Success).HasData(null);
        }

        // The console reads another customer's groups: the wall is named here, not taken from the call.
        var groups = await this.groups.Query()
            .IgnoreQueryFilters([Harmony.Core.BuildingBlocks.Infrastructure.QueryFilterNames.Tenant])
            .Where(g => g.ProvisioningConnectionId == connection.Id)
            .OrderBy(g => g.DisplayName)
            .Select(g => new DirectoryGroupModel
            {
                Id = g.Id,
                DisplayName = g.DisplayName,
                ExternalId = g.ExternalId,
                RoleId = g.RoleId,
                PropertyId = g.PropertyId,
                PropertyGroupId = g.PropertyGroupId,
                Members = g.Members.Count,
            })
            .ToListAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<DirectoryModel?>(eCallResponseStatus.Success).HasData(new DirectoryModel
        {
            Id = connection.Id,
            ClientId = connection.ClientId,
            IsEnabled = connection.IsEnabled,
            SecretIssuedAtUtc = connection.SecretIssuedAtUtc,
            ScimUrl = DirectoryCommandHandlerBase.ScimUrlOf(this.issuer),
            TokenUrl = DirectoryCommandHandlerBase.TokenUrlOf(this.issuer),
            People = await this.userAccounts.Query()
                .IgnoreQueryFilters([Harmony.Core.BuildingBlocks.Infrastructure.QueryFilterNames.Tenant])
                .CountAsync(u => u.ProvisioningConnectionId == connection.Id, cancellationToken),
            Groups = groups,
        });
    }
}
