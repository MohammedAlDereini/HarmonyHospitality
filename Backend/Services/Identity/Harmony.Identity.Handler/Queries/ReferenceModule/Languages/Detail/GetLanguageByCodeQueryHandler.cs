using Harmony.Identity.Domain.Models.ReferenceModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.Languages.Detail
{
    public class GetLanguageByCodeQueryHandler : IRequestHandler<GetLanguageByCodeQuery, CallResponse<LanguageModel>>
    {
        private readonly ILanguageRepository repository;

        public GetLanguageByCodeQueryHandler(ILanguageRepository repository)
        {
            this.repository = repository;
        }

        public async Task<CallResponse<LanguageModel>> Handle(GetLanguageByCodeQuery request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim().ToLowerInvariant();

            var model = await this.repository.Query()
                .Where(e => e.Code == code)
                .Select(ReferenceProjections.Language)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<LanguageModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Reference.NotFound));
            }

            return CallResponseBuilder.CreateResponse<LanguageModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
