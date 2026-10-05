namespace Harmony.Identity.Handler.Commands.ReferenceModule.RegulatoryEnvironments.Create;

using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;

public class CreateRegulatoryEnvironmentCommandHandler : IRequestHandler<CreateRegulatoryEnvironmentCommand, CallResponse<string>>
{
    private readonly IRegulatoryEnvironmentRepository repository;

    public CreateRegulatoryEnvironmentCommandHandler(IRegulatoryEnvironmentRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse<string>> Handle(CreateRegulatoryEnvironmentCommand command, CancellationToken cancellationToken)
    {
        // The entity trims and upper-cases the country, so every check below runs on the stored form.
        var environment = RegulatoryEnvironment.Create(command.Code, command.Kind, command.CountryCode, command.NameEn, command.NameAr, command.OwnerService);

        if (!await this.repository.CountryExistsAsync(environment.CountryCode, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.UnknownCountry));
        }

        // The key is compared the way the database compares it (case-insensitive), so JO-GST and jo-gst are one code.
        if (await this.repository.ExistsAsync(environment.Code, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.CodeInUse));
        }

        await this.repository.AddAsync(environment, cancellationToken);
        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.Created).HasData(environment.Code);
    }
}
