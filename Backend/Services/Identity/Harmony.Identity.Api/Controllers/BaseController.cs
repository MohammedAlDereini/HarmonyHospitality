namespace Harmony.Identity.Api.Controllers;

using Harmony.Core.Attributes;

/// <summary>
/// Base controller with common functionality for all controllers.
/// </summary>
[FrameworkController]
[Route("api/[controller]")]
public abstract class BaseController : ControllerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseController"/> class.
    /// </summary>
    /// <param name="mediator">Mediates commands and queries.</param>
    /// <param name="callResponseManager">Handles API responses.</param>
    /// <exception cref="ArgumentNullException">Thrown when any dependency is null.</exception>
    public BaseController(IMediator mediator, ICallResponseManager callResponseManager)
    {
        this.Mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        this.CallResponseManager = callResponseManager ?? throw new ArgumentNullException(nameof(callResponseManager));
    }

    /// <summary>
    /// Gets or sets mediatR instance for sending commands and queries.
    /// </summary>
    protected IMediator Mediator { get; set; }

    /// <summary>
    /// Gets or sets handles API responses in a standard way.
    /// </summary>
    protected ICallResponseManager CallResponseManager { get; set; }
}