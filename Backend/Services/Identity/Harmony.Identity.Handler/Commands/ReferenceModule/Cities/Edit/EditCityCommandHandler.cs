namespace Harmony.Identity.Handler.Commands.ReferenceModule.Cities.Edit;

using Harmony.Identity.Domain.Repositories;

public class EditCityCommandHandler : IRequestHandler<EditCityCommand, CallResponse>
{
    private readonly ICityRepository repository;

    public EditCityCommandHandler(ICityRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse> Handle(EditCityCommand command, CancellationToken cancellationToken)
    {
        var city = await this.repository.GetByIdAsync(command.Id, cancellationToken);

        if (city is null)
        {
            return Refused(BusinessErrorCodes.Identity.Reference.NotFound);
        }

        var subdivision = string.IsNullOrWhiteSpace(command.SubdivisionCode) ? null : command.SubdivisionCode.Trim().ToUpperInvariant();

        if (subdivision is not null && !await this.repository.SubdivisionBelongsAsync(subdivision, city.CountryCode, cancellationToken))
        {
            return Refused(BusinessErrorCodes.Identity.Reference.UnknownSubdivision);
        }

        if (!await this.repository.TimeZoneExistsAsync(command.TimeZoneId.Trim(), cancellationToken))
        {
            return Refused(BusinessErrorCodes.Identity.Reference.UnknownTimeZone);
        }

        if (await this.repository.NameExistsAsync(city.CountryCode, command.NameEn.Trim(), excludeId: city.Id, cancellationToken))
        {
            return Refused(BusinessErrorCodes.Identity.Reference.NameInUse);
        }

        city.Update(command.NameEn, command.NameAr, subdivision, command.TimeZoneId, command.Latitude, command.Longitude);
        city.SetActive(command.IsActive);

        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }

    private static CallResponse Refused(string code)
    {
        return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(code));
    }
}
