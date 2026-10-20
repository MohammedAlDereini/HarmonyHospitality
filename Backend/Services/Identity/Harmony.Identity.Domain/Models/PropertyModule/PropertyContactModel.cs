namespace Harmony.Identity.Domain.Models.PropertyModule;

/// <summary>A named person at the hotel, as the detail reads show it.</summary>
public class PropertyContactModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Role { get; set; }

    public string? Email { get; set; }

    public string? Telephone { get; set; }

    public bool IsPrimary { get; set; }
}
