using System.Linq.Expressions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Models.ConfigModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ConfigModule;

/// <summary>One registered key; unknown is 00110.</summary>
public class GetConfigSettingByKeyQuery : BaseQueryRequest<CallResponse<ConfigSettingModel>>
{
    public string Key { get; set; } = null!;

    public class GetConfigSettingByKeyQueryValidator : AbstractValidator<GetConfigSettingByKeyQuery>
    {
        public GetConfigSettingByKeyQueryValidator()
        {
            this.RuleFor(e => e.Key).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Config.KeyRequired);
        }
    }
}
