namespace Harmony.Identity.Handler.Commands.LookupModule.Values.Create;

using Harmony.Identity.Domain.Entities.Aggregates.LookupModule;
using Harmony.Identity.Domain.Repositories;

public class CreateLookupValueCommandHandler : LookupValueCommandHandlerBase, IRequestHandler<CreateLookupValueCommand, CallResponse>
{
    private readonly ILookupCategoryRepository lookupCategoryRepository;

    public CreateLookupValueCommandHandler(
        ILookupValueRepository lookupValueRepository,
        ILookupCategoryRepository lookupCategoryRepository)
        : base(lookupValueRepository)
    {
        this.lookupCategoryRepository = lookupCategoryRepository;
    }

    public async Task<CallResponse> Handle(CreateLookupValueCommand command, CancellationToken cancellationToken)
    {
        var category = await this.lookupCategoryRepository.Query()
            .FirstOrDefaultAsync(c => c.Id == command.LookupCategoryId, cancellationToken);

        if (category is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New("00002"));
        }

        if (command.IsGlobal && !category.IsGlobal)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New("00005"));
        }

        var name = command.Name.Trim();
        var normalized = LookupCategory.Normalize(name);

        if (await this.IsNameInUseInCategoryAsync(command.LookupCategoryId, normalized, excludeId: null, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New("00003"));
        }

        var description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();

        var value = command.IsGlobal
            ? LookupValue.NewGlobal(command.LookupCategoryId, name, description, command.IsActive)
            : LookupValue.New(command.LookupCategoryId, name, description, command.IsActive);

        await this.LookupValueRepository.AddAsync(value, cancellationToken);
        await this.LookupValueRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Created);
    }
}