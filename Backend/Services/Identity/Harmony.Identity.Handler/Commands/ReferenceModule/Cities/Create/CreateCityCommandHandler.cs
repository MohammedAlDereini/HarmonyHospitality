namespace Harmony.Identity.Handler.Commands.ReferenceModule.Cities.Create;

using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;

public class CreateCityCommandHandler : IRequestHandler<CreateCityCommand, CallResponse<Guid>>
{
    private readonly ICityRepository repository;

    public CreateCityCommandHandler(ICityRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse<Guid>> Handle(CreateCityCommand command, CancellationToken cancellationToken)
    {
        var country = command.CountryCode.Trim().ToUpperInvariant();
        var subdivision = string.IsNullOrWhiteSpace(command.SubdivisionCode) ? null : command.SubdivisionCode.Trim().ToUpperInvariant();

        if (!await this.repository.CountryExistsAsync(country, cancellationToken))
        {
            return Refused(BusinessErrorCodes.Identity.Reference.UnknownCountry);
        }

        // A subdivision of another country is as unknown as a made-up one: the entity's prefix rule never gets to throw.
        if (subdivision is not null && !await this.repository.SubdivisionBelongsAsync(subdivision, country, cancellationToken))
        {
            return Refused(BusinessErrorCodes.Identity.Reference.UnknownSubdivision);
        }

        if (!await this.repository.TimeZoneExistsAsync(command.TimeZoneId.Trim(), cancellationToken))
        {
            return Refused(BusinessErrorCodes.Identity.Reference.UnknownTimeZone);
        }

        var city = City.Create(country, subdivision, command.NameEn, command.NameAr, command.TimeZoneId, command.Latitude, command.Longitude);

        if (await this.repository.NameExistsAsync(city.CountryCode, city.NameEn, excludeId: null, cancellationToken))
        {
            return Refused(BusinessErrorCodes.Identity.Reference.NameInUse);
        }

        await this.repository.AddAsync(city, cancellationToken);
        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(city.Id);
    }

    private static CallResponse<Guid> Refused(string code)
    {
        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(code));
    }
}
