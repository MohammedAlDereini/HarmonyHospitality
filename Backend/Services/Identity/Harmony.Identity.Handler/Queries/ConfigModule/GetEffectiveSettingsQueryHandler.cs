using Harmony.Core.Abstractions;
using Harmony.Core.Identity.Settings;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Logging;
using Harmony.Identity.Domain.Models.ConfigModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ConfigModule;

public class GetEffectiveSettingsQueryHandler : IRequestHandler<GetEffectiveSettingsQuery, CallResponse<List<EffectiveSettingModel>>>
{
    private readonly IPropertyRepository properties;
    private readonly IConfigSettingRepository settings;
    private readonly IPropertyGroupMemberRepository members;
    private readonly IConfigValueRepository values;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;
    private readonly ICacheService cache;
    private readonly ILogger<GetEffectiveSettingsQueryHandler> logger;

    public GetEffectiveSettingsQueryHandler(
        IPropertyRepository properties,
        IConfigSettingRepository settings,
        IPropertyGroupMemberRepository members,
        IConfigValueRepository values,
        ICallContext callContext,
        TimeProvider clock,
        ICacheService cache,
        ILogger<GetEffectiveSettingsQueryHandler> logger)
    {
        this.properties = properties;
        this.settings = settings;
        this.members = members;
        this.values = values;
        this.callContext = callContext;
        this.clock = clock;
        this.cache = cache;
        this.logger = logger;
    }

    public async Task<CallResponse<List<EffectiveSettingModel>>> Handle(GetEffectiveSettingsQuery request, CancellationToken cancellationToken)
    {
        // The door demands the hotel ([RequireProperty]); the property-access check already let the caller act there.
        var propertyId = request.PropertyId ?? Guid.Parse(this.callContext.PropertyId());
        var key = string.IsNullOrWhiteSpace(request.Key) ? null : request.Key.Trim();

        if (key is not null)
        {
            var setting = await this.settings.Query().Where(s => s.Id == key).Select(s => new { s.IsActive }).FirstOrDefaultAsync(cancellationToken);
            if (setting is null)
            {
                return Fail(BusinessErrorCodes.Identity.Config.SettingNotFound);
            }

            if (!setting.IsActive)
            {
                return Fail(BusinessErrorCodes.Identity.Config.SettingInactive);
            }
        }

        var answer = await HotelSettings.ResolveAsync(this.properties, this.settings, this.members, this.values, propertyId, key, request.OwnerService, this.clock.GetUtcNow(), cancellationToken);
        if (answer is null)
        {
            return Fail(BusinessErrorCodes.Identity.Property.NotFound);
        }

        // The full set is what services read: keep it as the hotel's copy. A failed write costs a cache miss next time,
        // never a wrong answer (every change drops the copies), so the read still answers.
        if (key is null && string.IsNullOrWhiteSpace(request.OwnerService))
        {
            try
            {
                var copy = answer.Settings.Select(s => new EffectiveSetting { Key = s.Key, DataType = s.DataType, Outcome = s.Outcome, Value = s.Value }).ToList();
                await this.cache.AddAsync(SettingsCacheKeys.HotelSettingsKey(answer.TenantId, propertyId), copy, SettingsCacheKeys.HotelSettingsLifetime, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                this.logger.LogWarning(exception, "The settings copy of hotel {PropertyId} was not written.", propertyId);
            }
        }

        return CallResponseBuilder.CreateResponse<List<EffectiveSettingModel>>(eCallResponseStatus.Success).HasData(answer.Settings);
    }

    private static CallResponse<List<EffectiveSettingModel>> Fail(string code)
        => CallResponseBuilder.CreateResponse<List<EffectiveSettingModel>>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(code));
}
