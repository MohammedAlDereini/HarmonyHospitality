namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>On which floor a module is sold: per hotel (reservations, POS), or once for the whole customer (loyalty, multi-property).</summary>
public enum ModuleLevel
{
    Property = 1,
    Tenant = 2,
}
