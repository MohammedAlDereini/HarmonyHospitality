namespace Harmony.Identity.Handler.Commands.LookupModule.Categories.Create;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.LookupModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

public class CreateLookupCategoryCommandHandler : LookupCategoryCommandHandlerBase, IRequestHandler<CreateLookupCategoryCommand, CallResponse>
{
    private readonly ICallContext callContext;
    private readonly Guid platformTenantId;

    public CreateLookupCategoryCommandHandler(ILookupCategoryRepository lookupCategoryRepository, ICallContext callContext, IOptions<PlatformTenantSettings> platform)
        : base(lookupCategoryRepository)
    {
        this.callContext = callContext;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse> Handle(CreateLookupCategoryCommand command, CancellationToken cancellationToken)
    {
        // A global category shows in every hotel: only the platform tenant may publish one (00073). A hotel's own
        // categories stay its own, however many it makes.
        if (command.IsGlobal && !this.CallerIsPlatform())
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.PlatformOnly));
        }

        var name = command.Name.Trim();
        var normalized = LookupCategory.Normalize(name);

        if (await this.IsNameInUseAsync(normalized, excludeId: null, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Lookup.CategoryNameInUse));
        }

        var description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();

        var category = command.IsGlobal
            ? LookupCategory.NewGlobal(name, description)
            : LookupCategory.New(name, description);

        await this.LookupCategoryRepository.AddAsync(category, cancellationToken);
        await this.LookupCategoryRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Created);
    }

    /// <summary>The caller's tenant is the platform's; the token's tenant is the wall, as on the tenant doors.</summary>
    private bool CallerIsPlatform()
        => Guid.TryParse(this.callContext.TenantId(), out var tenant) && tenant == this.platformTenantId;
}