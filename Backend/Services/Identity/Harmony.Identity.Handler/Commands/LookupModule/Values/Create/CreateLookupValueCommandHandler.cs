namespace Harmony.Identity.Handler.Commands.LookupModule.Values.Create;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.LookupModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

public class CreateLookupValueCommandHandler : LookupValueCommandHandlerBase, IRequestHandler<CreateLookupValueCommand, CallResponse>
{
    private readonly ILookupCategoryRepository lookupCategoryRepository;
    private readonly ICallContext callContext;
    private readonly Guid platformTenantId;

    public CreateLookupValueCommandHandler(
        ILookupValueRepository lookupValueRepository,
        ILookupCategoryRepository lookupCategoryRepository,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform)
        : base(lookupValueRepository)
    {
        this.lookupCategoryRepository = lookupCategoryRepository;
        this.callContext = callContext;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse> Handle(CreateLookupValueCommand command, CancellationToken cancellationToken)
    {
        var category = await this.lookupCategoryRepository.Query()
            .FirstOrDefaultAsync(c => c.Id == command.LookupCategoryId, cancellationToken);

        if (category is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Lookup.CategoryNotFound));
        }

        // A global value shows in every hotel: only the platform tenant may publish one (00073).
        if (command.IsGlobal && !this.CallerIsPlatform())
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.PlatformOnly));
        }

        if (command.IsGlobal && !category.IsGlobal)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Lookup.GlobalValueInTenantCategory));
        }

        var name = command.Name.Trim();
        var normalized = LookupCategory.Normalize(name);

        if (await this.IsNameInUseInCategoryAsync(command.LookupCategoryId, normalized, excludeId: null, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Lookup.ValueNameInUse));
        }

        var description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();

        var value = command.IsGlobal
            ? LookupValue.NewGlobal(command.LookupCategoryId, name, description, command.IsActive)
            : LookupValue.New(command.LookupCategoryId, name, description, command.IsActive);

        await this.LookupValueRepository.AddAsync(value, cancellationToken);
        await this.LookupValueRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Created);
    }

    private bool CallerIsPlatform()
        => Guid.TryParse(this.callContext.TenantId(), out var tenant) && tenant == this.platformTenantId;
}