using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Devices;
using Microsoft.AspNetCore.Http;

namespace Harmony.Identity.Infrastructure.Services;

/// <summary>
/// Builds a trail row from the current request. Reads the caller and the request only; it never touches the database.
/// Who: the signed-in caller, else the account itself (a sign-in, a reset link), else "system" (no request at all).
/// </summary>
public sealed class SecurityEventFactory : ISecurityEventFactory
{
    public const string SystemActor = "system";

    private readonly ICallContext callContext;
    private readonly IHttpContextAccessor http;

    public SecurityEventFactory(ICallContext callContext, IHttpContextAccessor http)
    {
        this.callContext = callContext;
        this.http = http;
    }

    public SecurityAuditEvent Create(User user, string action)
    {
        var request = this.http.HttpContext;
        var actor = Guid.TryParse(this.callContext.UserId(), out var caller) ? caller.ToString("D")
            : request is not null ? user.Id.ToString("D")
            : SystemActor;

        return SecurityAuditEvent.Record(
            user.TenantId,
            user.Id,
            action,
            actor,
            AddressOf(request),
            request is null ? null : DeviceLabel.From(request.Request.Headers.UserAgent),
            DateTimeOffset.UtcNow);
    }

    /// <summary>The caller's address as the server saw it (behind a proxy this is the proxy until forwarded headers are on).</summary>
    private static string? AddressOf(HttpContext? request)
    {
        var address = request?.Connection.RemoteIpAddress;
        if (address is null)
        {
            return null;
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }
}
