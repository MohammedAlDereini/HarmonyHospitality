namespace Harmony.Identity.Handler.Commands.ReferenceModule.TimeZones.Create;

using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;

public class CreateTimeZoneCommandHandler : IRequestHandler<CreateTimeZoneCommand, CallResponse<string>>
{
    private readonly ITimeZoneRepository repository;

    public CreateTimeZoneCommandHandler(ITimeZoneRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse<string>> Handle(CreateTimeZoneCommand command, CancellationToken cancellationToken)
    {
        if (!TimeZoneRef.IsResolvable(command.Id))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.TimeZoneNotResolvable));
        }

        var timeZone = TimeZoneRef.Create(command.Id, command.NameEn, command.NameAr);

        if (await this.repository.ExistsAsync(timeZone.Id, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.CodeInUse));
        }

        await this.repository.AddAsync(timeZone, cancellationToken);
        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.Created).HasData(timeZone.Id);
    }
}
