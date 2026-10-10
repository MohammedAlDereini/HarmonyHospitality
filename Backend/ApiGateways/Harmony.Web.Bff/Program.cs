using Harmony.Core.DI;
using Harmony.Web.Bff.Settings;

var builder = WebApplication.CreateBuilder(args);

// The framework's one-file settings: applicationSettings(.Environment).json from the run folder, like every service.
builder.Configuration.ConfigureCoreConfiguration(builder.Environment);

// Nothing tells a caller which server software to aim at.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

builder.Services.AddOptions<BffSettings>()
    .Bind(builder.Configuration.GetSection(BffSettings.Section))
    .Validate(BffSettings.IsValid, $"{BffSettings.Section}: Authority and ApiDestination must be absolute https URLs, the private key file must exist, the cookie must keep its __Host- prefix, SessionIdleMinutes 5 to 720, SessionAbsoluteHours 1 to 24.")
    .ValidateOnStart();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "alive" }));

await app.RunAsync();
