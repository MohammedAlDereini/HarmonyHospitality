namespace Harmony.Identity.Handler.Commands.UserAccountModule.Sessions;

/// <summary>The caller ends their own sessions on every device. Who: the token's subject; the body carries nothing.</summary>
public class LogoutEverywhereCommand : BaseCommandRequest<CallResponse>
{
}
