using System.Linq.Expressions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Models.ConfigModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ConfigModule;

public class GetPagedConfigSettingsQueryHandler : IRequestHandler<GetPagedConfigSettingsQuery, CallResponse<PagedResult<ConfigSettingModel>>>
{
    private readonly IConfigSettingRepository settings;

    public GetPagedConfigSettingsQueryHandler(IConfigSettingRepository settings)
    {
        this.settings = settings;
    }

    public async Task<CallResponse<PagedResult<ConfigSettingModel>>> Handle(GetPagedConfigSettingsQuery request, CancellationToken cancellationToken)
    {
        var query = this.settings.Query();
        if (!string.IsNullOrWhiteSpace(request.OwnerService))
        {
            var owner = request.OwnerService.Trim();
            query = query.Where(s => s.OwnerService == owner);
        }

        if (request.SearchText is { } text && text.Trim().Length >= 2)
        {
            var search = text.Trim();
            query = query.Where(s => s.Id.Contains(search) || s.OwnerService.Contains(search));
        }

        if (request.IsActive is { } active)
        {
            query = query.Where(s => s.IsActive == active);
        }

        var page = await query.Select(ConfigSettingProjections.Model).GetPagedResultAsync(request, true, cancellationToken);
        foreach (var row in page.Results)
        {
            row.Split();
        }

        return CallResponseBuilder.CreateResponse<PagedResult<ConfigSettingModel>>(eCallResponseStatus.Success).HasData(page);
    }
}
