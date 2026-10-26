namespace Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;

/// <summary>
/// Who wins when two groups set the same standard for a hotel: the higher number. A number the customer controls,
/// where Opera has a fixed two-level rule; every group starts at the floor. Equal precedence is a tie the standards
/// resolver reports, never a coin toss (design 5.1). One column on the group's row.
/// </summary>
public sealed record Precedence
{
    public const int Min = 0;
    public const int Max = 1000;

    public Precedence(int value)
    {
        if (value is < Min or > Max)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Precedence runs {Min} to {Max}.");
        }

        Value = value;
    }

    public int Value { get; }

    public static Precedence Floor => new(Min);

    public bool Outranks(Precedence other) => Value > other.Value;
}
