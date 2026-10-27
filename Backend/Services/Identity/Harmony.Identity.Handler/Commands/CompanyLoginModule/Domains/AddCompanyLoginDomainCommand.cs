namespace Harmony.Identity.Handler.Commands.CompanyLoginModule.Domains;

/// <summary>The customer's administrator lists one more of the company's e-mail domains (a brand, a second company name).</summary>
public class AddCompanyLoginDomainCommand : BaseCommandRequest<CallResponse>
{
    /// <summary>The company login.</summary>
    public Guid Id { get; set; }

    public string Domain { get; set; } = null!;

    public class AddCompanyLoginDomainCommandValidation : AbstractValidator<AddCompanyLoginDomainCommand>
    {
        public AddCompanyLoginDomainCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.CompanyLogin.IdRequired);
            CompanyLoginRules.Domain(this, e => e.Domain);
        }
    }
}

/// <summary>The customer's administrator takes an e-mail domain off the list. Its people sign in with a Harmony password again.</summary>
public class RemoveCompanyLoginDomainCommand : BaseCommandRequest<CallResponse>
{
    /// <summary>The company login.</summary>
    public Guid Id { get; set; }

    public string Domain { get; set; } = null!;

    public class RemoveCompanyLoginDomainCommandValidation : AbstractValidator<RemoveCompanyLoginDomainCommand>
    {
        public RemoveCompanyLoginDomainCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.CompanyLogin.IdRequired);
            CompanyLoginRules.Domain(this, e => e.Domain);
        }
    }
}
