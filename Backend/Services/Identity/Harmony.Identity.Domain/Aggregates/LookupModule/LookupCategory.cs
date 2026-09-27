using System.Text;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Aggregates.LookupModule;

public sealed class LookupCategory : BaseEntity<Guid>, IAuditableEntity, IMultiTenantEntity, ISoftDeleteEntity, IConcurrentEntity
{
    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    public string? Description { get; private set; }

    public bool IsGlobal { get; private set; }

    public Guid TenantId { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public bool IsDeleted { get; set; }

    public static LookupCategory New(string name, string? description)
    {
        return new LookupCategory
        {
            Name = name,
            NormalizedName = Normalize(name),
            Description = description,
            IsGlobal = false,
        };
    }

    public static LookupCategory NewGlobal(string name, string? description)
    {
        return new LookupCategory
        {
            Name = name,
            NormalizedName = Normalize(name),
            Description = description,
            IsGlobal = true,
        };
    }

    public void Update(string name, string? description)
    {
        this.Name = name;
        this.NormalizedName = Normalize(name);
        this.Description = description;
    }

    public void Delete()
    {
        this.IsDeleted = true;
    }

    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(name.Length);

        foreach (var ch in name)
        {
            if (!char.IsWhiteSpace(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }
}