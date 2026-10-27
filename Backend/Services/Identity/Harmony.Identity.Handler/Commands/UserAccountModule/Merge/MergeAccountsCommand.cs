namespace Harmony.Identity.Handler.Commands.UserAccountModule.Merge;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.MergeApprovalModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.ApprovalModule;
using Microsoft.AspNetCore.Identity;

/// <summary>Two accounts of one person: the second is merged into the first.</summary>
public class MergeAccountsCommand : BaseCommandRequest<CallResponse>
{
    /// <summary>The account kept.</summary>
    public Guid KeepId { get; set; }

    /// <summary>The account merged into it: terminated, pointing at the one kept, never deleted.</summary>
    public Guid MergeId { get; set; }

    public string? Reason { get; set; }

    public class MergeAccountsCommandValidation : AbstractValidator<MergeAccountsCommand>
    {
        public MergeAccountsCommandValidation()
        {
            this.RuleFor(e => e.KeepId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Approval.MergeAccountsRequired);
            this.RuleFor(e => e.MergeId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Approval.MergeAccountsRequired);
        }
    }
}

/// <summary>
/// Both accounts must be the caller's customer's, active (not terminated, not merged before) and within the caller's rank.
/// The merged one ends everywhere at once (its version bumps); the merge record names both, and the approval when there
/// was one. Through an approval when the customer requires approvals.
/// </summary>
public class MergeAccountsCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<MergeAccountsCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly IMergeApprovalRepository merges;
    private readonly ICallContext callContext;
    private readonly IApprovalGate gate;
    private readonly TimeProvider clock;

    public MergeAccountsCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IUserAccountRepository userAccounts, IMergeApprovalRepository merges, ICallContext callContext, IApprovalGate gate, TimeProvider clock)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.userAccounts = userAccounts;
        this.merges = merges;
        this.callContext = callContext;
        this.gate = gate;
        this.clock = clock;
    }

    public async Task<CallResponse> Handle(MergeAccountsCommand command, CancellationToken cancellationToken)
    {
        if (command.KeepId == command.MergeId)
        {
            return Fail(BusinessErrorCodes.Identity.Approval.MergeSameAccount);
        }

        var kept = await this.LoadAsync(command.KeepId);
        var merged = await this.LoadAsync(command.MergeId);
        if (kept is null || merged is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (kept.State == AdministrativeState.Terminated || merged.State == AdministrativeState.Terminated || kept.SupersededByUserAccountId is not null)
        {
            return Fail(BusinessErrorCodes.Identity.Approval.MergeNotActive);
        }

        foreach (var account in new[] { kept, merged })
        {
            if (await RankRules.RefusalAsync(this.userAccounts, this.callContext, account, cancellationToken) is { } refusal)
            {
                return Fail(refusal);
            }
        }

        if (await this.gate.HoldAsync(ApprovalKinds.AccountMerge, command, merged.Id, [kept.Id, merged.Id], cancellationToken) is { } held)
        {
            return Fail(held);
        }

        merged.SupersedeBy(kept.Id, string.IsNullOrWhiteSpace(command.Reason) ? "Merged into another account of the same person." : command.Reason);
        await this.merges.AddAsync(MergeApproval.Record(merged.TenantId, kept.Id, merged.Id, this.gate.Executing, this.clock.GetUtcNow()), cancellationToken);
        await this.TrailAsync(merged, SecurityAuditActions.AccountMerged, cancellationToken);
        await this.TrailAsync(kept, SecurityAuditActions.AccountMerged, cancellationToken);
        await this.SaveAsync(merged);
        await this.PublishSecurityVersionAsync(merged, cancellationToken);
        return Ok();
    }
}
