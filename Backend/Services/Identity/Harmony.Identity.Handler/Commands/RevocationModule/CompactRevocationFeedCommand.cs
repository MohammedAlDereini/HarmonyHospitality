namespace Harmony.Identity.Handler.Commands.RevocationModule;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Repositories;

/// <summary>
/// Compacts one customer's feed now instead of waiting for the hourly job: lines older than KeepDays go into a snapshot
/// and a tombstone marks the cut. Platform only; the job keeps 7 days.
/// </summary>
public class CompactRevocationFeedCommand : BaseCommandRequest<CallResponse<RevocationCompaction>>
{
    public const int DefaultKeepDays = 7;

    public Guid TenantId { get; set; }

    public int KeepDays { get; set; } = DefaultKeepDays;

    public class CompactRevocationFeedCommandValidation : AbstractValidator<CompactRevocationFeedCommand>
    {
        public CompactRevocationFeedCommandValidation()
        {
            this.RuleFor(e => e.TenantId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);
        }
    }
}

public class CompactRevocationFeedCommandHandler : IRequestHandler<CompactRevocationFeedCommand, CallResponse<RevocationCompaction>>
{
    private readonly IRevocationFeedRepository feed;
    private readonly TimeProvider clock;

    public CompactRevocationFeedCommandHandler(IRevocationFeedRepository feed, TimeProvider clock)
    {
        this.feed = feed;
        this.clock = clock;
    }

    public async Task<CallResponse<RevocationCompaction>> Handle(CompactRevocationFeedCommand request, CancellationToken cancellationToken)
    {
        var now = this.clock.GetUtcNow();
        var keep = TimeSpan.FromDays(Math.Clamp(request.KeepDays, 0, 30));
        var result = await this.feed.CompactAsync(request.TenantId, now - keep, now, cancellationToken);
        return CallResponseBuilder.CreateResponse<RevocationCompaction>(eCallResponseStatus.Success).HasData(result);
    }
}
