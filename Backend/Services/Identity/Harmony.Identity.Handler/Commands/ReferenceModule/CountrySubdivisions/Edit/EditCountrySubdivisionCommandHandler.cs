namespace Harmony.Identity.Handler.Commands.ReferenceModule.CountrySubdivisions.Edit;

using Harmony.Identity.Domain.Repositories;

public class EditCountrySubdivisionCommandHandler : IRequestHandler<EditCountrySubdivisionCommand, CallResponse>
{
    private readonly ICountrySubdivisionRepository repository;

    public EditCountrySubdivisionCommandHandler(ICountrySubdivisionRepository repository)
    {
        this.repository = repository;
    }

    public async Task<CallResponse> Handle(EditCountrySubdivisionCommand command, CancellationToken cancellationToken)
    {
        var subdivision = await this.repository.GetByCodeAsync(command.Code.Trim().ToUpperInvariant(), cancellationToken);

        if (subdivision is null)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
        }

        subdivision.Update(command.NameEn, command.NameAr, command.Kind);

        await this.repository.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
