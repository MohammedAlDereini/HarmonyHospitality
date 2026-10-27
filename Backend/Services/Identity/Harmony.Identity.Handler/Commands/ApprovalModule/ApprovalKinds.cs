namespace Harmony.Identity.Handler.Commands.ApprovalModule;

using Harmony.Identity.Handler.Commands.CompanyLoginModule.Switch;
using Harmony.Identity.Handler.Commands.UserAccountModule.Access;
using Harmony.Identity.Handler.Commands.UserAccountModule.Merge;
using Harmony.Identity.Handler.Commands.UserAccountModule.Roles;
using Harmony.Identity.Handler.Commands.UserAccountModule.TwoStepExemption;

/// <summary>
/// The changes that wait for a second person when the customer requires approvals, and the command each one runs once
/// approved. A fixed list: an approval never runs anything that is not named here.
/// </summary>
public static class ApprovalKinds
{
    public const string AdministratorRoles = "account.administrator-roles";
    public const string AdministratorAssignment = "account.administrator-assignment";
    public const string TwoStepExemption = "account.two-step-exemption";
    public const string AccountMerge = "account.merge";
    public const string CompanyLoginOff = "company-login.switch-off";
    public const string ApprovalsOff = "approvals.switch-off";

    public static Type? CommandOf(string kind) => kind switch
    {
        AdministratorRoles => typeof(UpdateUserRolesCommand),
        AdministratorAssignment => typeof(AssignRoleCommand),
        TwoStepExemption => typeof(ExemptFromTwoStepCommand),
        AccountMerge => typeof(MergeAccountsCommand),
        CompanyLoginOff => typeof(SwitchCompanyLoginCommand),
        ApprovalsOff => typeof(SetApprovalsRequiredCommand),
        _ => null,
    };
}
