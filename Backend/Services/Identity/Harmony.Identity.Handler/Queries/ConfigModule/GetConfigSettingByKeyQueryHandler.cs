using System.Linq.Expressions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Models.ConfigModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ConfigModule;

public class GetConfigSettingByKeyQueryHandler : IRequestHandler<GetConfigSettingByKeyQuery, CallResponse<ConfigSettingModel>>
{
    private readonly IConfigSettingRepository settings;

    public GetConfigSettingByKeyQueryHandler(IConfigSettingRepository settings)
    {
        this.settings = settings;
    }

    public async Task<CallResponse<ConfigSettingModel>> Handle(GetConfigSettingByKeyQuery request, CancellationToken cancellationToken)
    {
        var key = request.Key.Trim();
        var model = await this.settings.Query().Where(s => s.Id == key).Select(ConfigSettingProjections.Model).FirstOrDefaultAsync(cancellationToken);
        return model is null
            ? CallResponseBuilder.CreateResponse<ConfigSettingModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Config.SettingNotFound))
            : CallResponseBuilder.CreateResponse<ConfigSettingModel>(eCallResponseStatus.Success).HasData(model.Split());
    }
}
