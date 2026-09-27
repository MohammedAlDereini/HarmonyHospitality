namespace Harmony.Identity.Handler.Commands.LookupModule.Values.Delete;

using Harmony.Identity.Domain.Repositories;

public class DeleteLookupValueCommandHandler : IRequestHandler<DeleteLookupValueCommand, CallResponse>
{
    private readonly ILookupValueRepository lookupValueRepository;

    public DeleteLookupValueCommandHandler(ILookupValueRepository lookupValueRepository)
    {
        this.lookupValueRepository = lookupValueRepository;
    }

    public async Task<CallResponse> Handle(DeleteLookupValueCommand command, CancellationToken cancellationToken)
    {
        var value = await this.lookupValueRepository.GetByIdAsync(command.Id, cancellationToken);

        if (value is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New("00004"));
        }

        value.Delete();

        await this.lookupValueRepository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}