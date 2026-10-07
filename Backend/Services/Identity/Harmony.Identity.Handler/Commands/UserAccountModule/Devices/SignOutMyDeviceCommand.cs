namespace Harmony.Identity.Handler.Commands.UserAccountModule.Devices;

/// <summary>The caller signs out of one of their own devices. Who: the token's subject; the body carries only the device.</summary>
public class SignOutMyDeviceCommand : BaseCommandRequest<CallResponse>
{
    public Guid DeviceId { get; set; }

    public class SignOutMyDeviceCommandValidation : AbstractValidator<SignOutMyDeviceCommand>
    {
        public SignOutMyDeviceCommandValidation()
        {
            this.RuleFor(e => e.DeviceId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.DeviceIdRequired);
        }
    }
}
