namespace Harmony.Identity.Handler.Commands.CompanyLoginModule.Add;

/// <summary>
/// The platform adds a company login to a customer ("the company chose Microsoft, we set it"): the provider and one of the
/// company's e-mail domains. For Microsoft the domain also finds the company's own tenant. It starts switched off; the
/// customer's administrator lists more domains and switches it on.
/// </summary>
public class AddCompanyLoginCommand : BaseCommandRequest<CallResponse<Guid>>
{
    public Guid TenantId { get; set; }

    /// <summary>Microsoft or Google.</summary>
    public string Provider { get; set; } = null!;

    /// <summary>One of the company's e-mail domains, like ritz.com.</summary>
    public string Domain { get; set; } = null!;

    public class AddCompanyLoginCommandValidation : AbstractValidator<AddCompanyLoginCommand>
    {
        public AddCompanyLoginCommandValidation()
        {
            this.RuleFor(e => e.TenantId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.CompanyLogin.TenantRequired);
            this.RuleFor(e => e.Provider)
                .Must(text => CompanyLoginRules.ProviderOf(text) is not null).WithErrorCode(ModelValidationErrorCodes.Identity.CompanyLogin.ProviderInvalid);
            CompanyLoginRules.Domain(this, e => e.Domain);
        }
    }
}
