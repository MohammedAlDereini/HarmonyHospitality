namespace Harmony.Identity.Handler.Commands.UserAccountModule.Devices;

/// <summary>An admin signs a person out of one device (id and device in the body, like Suspend).</summary>
public class SignOutDeviceCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public class SignOutDeviceCommandValidation : AbstractValidator<SignOutDeviceCommand>
    {
        public SignOutDeviceCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);

            this.RuleFor(e => e.DeviceId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.DeviceIdRequired);
        }
    }
}
