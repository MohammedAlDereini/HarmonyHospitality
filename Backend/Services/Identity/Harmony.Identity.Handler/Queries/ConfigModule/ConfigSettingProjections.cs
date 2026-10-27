using System.Linq.Expressions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Models.ConfigModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ConfigModule;

public static class ConfigSettingProjections
{
    public static readonly Expression<Func<ConfigSetting, ConfigSettingModel>> Model = s => new ConfigSettingModel
    {
        Key = s.Id,
        OwnerService = s.OwnerService,
        DataType = s.DataType,
        SystemDefault = s.SystemDefault,
        Narrowing = s.AppliesToTypes,
        IsActive = s.IsActive,
    };
}
