using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.ConfigModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ConfigModule;

/// <summary>
/// What every active key resolves to at the hotel the call names (X-Harmony-Property), each with its trace: the
/// door a service reads its settings through, and the screen that shows a person why. Narrowed to one key (an
/// unknown key is 00110, a switched-off one 00112) or to one owning service.
/// </summary>
public class GetEffectiveSettingsQuery : BaseQueryRequest<CallResponse<List<EffectiveSettingModel>>>
{
    /// <summary>One key only.</summary>
    public string? Key { get; set; }

    /// <summary>Only the keys of this service, like FrontOffice.</summary>
    public string? OwnerService { get; set; }

    /// <summary>
    /// The hotel, for Identity's own settings source only. The door never binds it (the controller builds the query from
    /// key and ownerService); a call over HTTP is always about the hotel it names in X-Harmony-Property.
    /// </summary>
    public Guid? PropertyId { get; set; }

    public class GetEffectiveSettingsQueryValidator : AbstractValidator<GetEffectiveSettingsQuery>
    {
        public GetEffectiveSettingsQueryValidator()
        {
            this.RuleFor(e => e.Key)
                .MaximumLength(Domain.Entities.Aggregates.ConfigModule.ConfigSetting.KeyMaxLength)
                .WithErrorCode(ModelValidationErrorCodes.Identity.Config.KeyInvalidFormat);
        }
    }
}
