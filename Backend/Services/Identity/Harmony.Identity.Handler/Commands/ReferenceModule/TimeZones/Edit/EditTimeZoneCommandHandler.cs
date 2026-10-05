namespace Harmony.Identity.Handler.Commands.ReferenceModule.TimeZones.Edit;

using Harmony.Identity.Domain.Repositories;

public class EditTimeZoneCommandHandler : IRequestHandler<EditTimeZoneCommand, CallResponse>
{
    private readonly ITimeZoneRepository repository;

    public EditTimeZoneCommandHandler(ITimeZoneRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse> Handle(EditTimeZoneCommand command, CancellationToken cancellationToken)
    {
        var timeZone = await this.repository.GetByIdAsync(command.Id.Trim(), cancellationToken);

        if (timeZone is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
        }

        timeZone.Update(command.NameEn, command.NameAr);
        timeZone.SetActive(command.IsActive);

        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
