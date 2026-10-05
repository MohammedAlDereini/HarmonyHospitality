namespace Harmony.Identity.Handler.Commands.ReferenceModule.CountrySubdivisions.Create;

using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;

public class CreateCountrySubdivisionCommandHandler : IRequestHandler<CreateCountrySubdivisionCommand, CallResponse<string>>
{
    private readonly ICountrySubdivisionRepository repository;

    public CreateCountrySubdivisionCommandHandler(ICountrySubdivisionRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse<string>> Handle(CreateCountrySubdivisionCommand command, CancellationToken cancellationToken)
    {
        var subdivision = CountrySubdivision.Create(command.CountryCode, command.Code, command.NameEn, command.NameAr, command.Kind);

        if (!await this.repository.CountryExistsAsync(subdivision.CountryCode, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.UnknownCountry));
        }

        if (await this.repository.ExistsAsync(subdivision.Code, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.CodeInUse));
        }

        await this.repository.AddAsync(subdivision, cancellationToken);
        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<string>(eCallResponseStatus.Created).HasData(subdivision.Code);
    }
}
