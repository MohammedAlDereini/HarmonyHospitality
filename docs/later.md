# Later

Work we decided to do, parked on purpose. Each entry says why it matters, what was decided, what is ready, and what it waits for.
When an item is done, delete it from this file.

---

## L1. Swagger behind a user name and password

**Parked:** 2026-10-05, during the Identity security hardening.

**Why.** Swagger UI shows every endpoint, parameter and model of a service. Today the framework serves it to anyone, in every
environment, before any sign-in check. In production that is a free map of the system (OWASP API Security Top 10, API9).

**Decided.**
- Swagger opens only after the browser asks for a user name and password (HTTP Basic, the way the company already works).
- No `ApplicationSettings:ApiDocs` section in a service's settings means no Swagger at all.
- Development, qa and uat have the section; prod leaves it out.
- Basic auth sends the password readable (base64), so the docs refuse plain HTTP (RFC 7617 §4).

**Waits for.** The forwarded-headers change (hardening item 5). Behind the production TLS proxy the service only knows a request
was HTTPS once forwarded headers are on; before that, qa and uat would answer 403 on the docs (it fails closed, nothing leaks).

**Accepted tradeoffs.** One shared login, so no per-person record of who opened the docs. A new password needs a restart.

### The change, file by file

**1. New file:** `Harmony.Framework\Harmony.Core\Implementations\ApiDocsCredentials.cs`

```csharp
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Harmony.Core.Implementations
{
    /// <summary>
    /// The user name and password that open a service's API docs (Swagger), from ApplicationSettings:ApiDocs.
    /// No section: no docs. A section with a missing user name or a short password stops the service at startup.
    /// Compared in constant time, so how long the answer takes tells nothing about how close a guess was.
    /// </summary>
    internal sealed class ApiDocsCredentials
    {
        internal const string Section = "ApplicationSettings:ApiDocs";
        internal const int MinimumPasswordLength = 32;

        private readonly byte[] expected;

        private ApiDocsCredentials(string userName, string password)
        {
            expected = SHA256.HashData(Encoding.UTF8.GetBytes($"{userName}:{password}"));
        }

        /// <summary>The credentials, or null when the section is absent (docs off).</summary>
        internal static ApiDocsCredentials Read(IConfiguration configuration)
        {
            var section = configuration.GetSection(Section);
            if (!section.Exists())
                return null;

            var userName = section["UserName"]?.Trim();
            var passwordPath = section["PasswordPath"];
            var password = !string.IsNullOrWhiteSpace(passwordPath) && File.Exists(passwordPath)
                ? File.ReadAllText(passwordPath).Trim()
                : string.Empty;

            if (string.IsNullOrEmpty(userName) || userName.Contains(':') || password.Length < MinimumPasswordLength)
                throw new InvalidOperationException(
                    $"{Section}: UserName (without ':') and a PasswordPath file holding a generated secret of at least {MinimumPasswordLength} characters are required. Remove the section to turn the API docs off.");

            return new ApiDocsCredentials(userName, password);
        }

        /// <summary>True only for "Authorization: Basic base64(user:password)" carrying exactly these credentials.</summary>
        internal bool Accepts(string authorization)
        {
            const string scheme = "Basic ";
            if (authorization is null || !authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                return false;

            string pair;
            try
            {
                pair = Encoding.UTF8.GetString(Convert.FromBase64String(authorization.Substring(scheme.Length).Trim()));
            }
            catch (FormatException)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(pair)), expected);
        }
    }
}
```

**2. Edit:** `Harmony.Framework\Harmony.Core\DI\DependenciesConfigurator.cs`

(a) Add with the other `using` lines:

```csharp
using Microsoft.Extensions.Logging;
```

(b) Replace the whole `internal static IApplicationBuilder UseSwagger(this IApplicationBuilder builder)` method:

```csharp
        internal static IApplicationBuilder UseSwagger(this IApplicationBuilder builder)
        {
            if (builder.IsNull())
                throw new ArgumentNullException(nameof(builder));

            var logger = builder.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("Harmony.Core.ApiDocs");

            // The docs open only with the user name and password of ApplicationSettings:ApiDocs. No section: no docs at all.
            var credentials = ApiDocsCredentials.Read(builder.ApplicationServices.GetRequiredService<IConfiguration>());
            if (credentials.IsNull())
            {
                logger.LogInformation("API docs are off: no {Section} in the settings.", ApiDocsCredentials.Section);
                return builder;
            }

            IApiVersionDescriptionProvider provider = builder.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();
            AppInfo appInfo = builder.ApplicationServices.GetRequiredService<AppInfo>();
            string docsPath = $"/api/{appInfo.Name.ToLower()}/swagger";

            // Every request under the docs path (the page and the swagger.json) must carry the credentials.
            builder.UseWhen(
                context => context.Request.Path.StartsWithSegments(docsPath),
                docs => docs.Use(async (context, next) =>
                {
                    // Basic auth sends the password readable (base64, RFC 7617 §4): never over plain HTTP.
                    if (!context.Request.IsHttps)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return;
                    }

                    // Nobody keeps a copy of the API map: not the browser cache, not a proxy.
                    context.Response.Headers.CacheControl = "no-store";

                    if (credentials.Accepts(context.Request.Headers.Authorization.ToString()))
                    {
                        await next(context);
                        return;
                    }

                    // A first visit carries no credentials yet; anything else is a wrong guess worth a log line.
                    if (!string.IsNullOrEmpty(context.Request.Headers.Authorization))
                        logger.LogWarning("API docs: wrong credentials from {RemoteIp}.", context.Connection.RemoteIpAddress);

                    // 401 + WWW-Authenticate makes the browser show its user name / password box.
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers.WWWAuthenticate = "Basic realm=\"API docs\", charset=\"UTF-8\"";
                }));

            builder.UseSwagger(swaggerOptions => swaggerOptions.RouteTemplate = $"api/{appInfo.Name.ToLower()}/swagger/{{documentName}}/swagger.json");
            builder.UseSwaggerUI(options =>
            {
                foreach (var description in provider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint($"/api/{appInfo.Name.ToLower()}/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                    options.RoutePrefix = $"api/{appInfo.Name.ToLower()}/swagger";
                }
            });

            return builder;
        }
```

**3. New secret file:** `Harmony\secrets\identity-apidocs-password.txt` (a generated 32-character secret):

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) | Set-Content -NoNewline 'D:\PMS System\Harmony\secrets\identity-apidocs-password.txt'
```

**4. Edit:** `Harmony\Harmony.slnx`, inside `<Folder Name="/SolutionItems/secrets/">`:

```xml
    <File Path="secrets/identity-apidocs-password.txt" />
```

**5. Edit the settings** in `Harmony\Backend\Services\Identity\Harmony.Identity.Api\`, inside `"ApplicationSettings"`, after `"ConnectionStrings"`:

`applicationSettings.json` and `applicationSettings.Development.json`:

```json
    "ApiDocs": {
      "UserName": "harmony-docs",
      "PasswordPath": "D:/PMS System/Harmony/secrets/identity-apidocs-password.txt"
    }
```

`applicationSettings.qa.json` and `applicationSettings.uat.json`:

```json
    "ApiDocs": {
      "UserName": "harmony-docs",
      "PasswordPath": "/run/secrets/identity-apidocs-password"
    }
```

`applicationSettings.prod.json`: no `ApiDocs` section.

### Tests

1. The framework builds with no errors.
2. Opening the Swagger page makes the browser ask for a user name and password.
3. A wrong password shows the box again, and one warning line appears in the log with no password in it.
4. The right password opens Swagger; "Try it out" works with the Bearer token as before.
5. Opening the docs over plain `http://` answers 403.
6. With the password file deleted, the service refuses to start and names `ApplicationSettings:ApiDocs`.
7. With the `ApiDocs` section removed, the service starts, logs "API docs are off", and the Swagger address answers 404.

### Sources

- Microsoft Learn, Generate OpenAPI documents (.NET 10), "Limit OpenAPI document access to authorized users":
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0
- OWASP API Security Top 10 (2023), API9 Improper Inventory Management: https://owasp.org/API-Security/editions/2023/en/0x11-t10/
- RFC 7617, The Basic HTTP Authentication Scheme: https://www.rfc-editor.org/rfc/rfc7617

---

## L2. The front door: Caddy or Azure Front Door

**Parked:** 2026-10-05. We build Identity and YARP first; the public edge is planned when we deploy.

**The picture.**

```
 browser ══HTTPS══► EDGE (Caddy or Azure Front Door) ──HTTPS──► YARP (BFF + gateway) ──mTLS──► services
                                                   ──HTTPS──► Identity (sign-in pages)
```

**Decide first: which edge.**

| Option | Good for | Notes |
|---|---|---|
| Caddy | Our own server (VPS) | Free; public certificates obtained and renewed by itself; about 5 lines per site; already used for Sawaeer |
| Azure Front Door | When Harmony moves to Azure | Paid; managed certificates, global edge, WAF; YARP stays behind it unchanged |

**What the edge must do (either option).**
1. **Public HTTPS certificates**, renewed automatically.
2. **HSTS, here and only here** (one place to change it). Caddy:
   `header Strict-Transport-Security "max-age=31536000; includeSubDomains"`.
   The apps do not call `UseHsts()`: behind the edge they never see HTTPS, and Microsoft says the app needs no HSTS
   middleware when the proxy writes the header.
3. **HTTP:** web pages redirect to HTTPS (people typing the address); `/api` addresses refuse HTTP with an error, so a
   misconfigured client fails loudly instead of sending a token or password in clear first (Microsoft: don't redirect web APIs).
4. **Pass on the client's real IP and "this came in as HTTPS"** (`X-Forwarded-For`, `X-Forwarded-Proto`).

**What the apps must do: forwarded headers, believing only the edge (and YARP for the hop to Identity's API).**
Without it, Identity thinks every request is plain HTTP from the edge's IP: Duende's discovery document shows `http://`
addresses, logs and per-IP rate limits see one IP for everyone. Microsoft: forwarded headers middleware "isn't enabled by
default" (except behind IIS) and "only allow trusted proxies and networks to forward headers. Otherwise, IP spoofing attacks
are possible."

Ready code for `Harmony\Backend\Services\Identity\Harmony.Identity.Api\Program.cs` (same idea later for the YARP host):

(a) `using Microsoft.AspNetCore.HttpOverrides;` at the top.

(b) In `Main`, after `ConfigureAppServices(builder);`: `ConfigureForwardedHeaders(builder);`

(c) The local function:

```csharp
        // Behind the edge: X-Forwarded-For / -Proto are believed only from the proxies named in ForwardedHeadersSettings,
        // never from the caller (a forged header would fake the client IP or make a plain HTTP hop look secure).
        // Read and checked here, at startup: a wrong address stops the service instead of failing on the first request.
        static void ConfigureForwardedHeaders(WebApplicationBuilder builder)
        {
            const string section = "ForwardedHeadersSettings";

            var proxies = (builder.Configuration.GetSection($"{section}:KnownProxies").Get<string[]>() ?? [])
                .Select(proxy => IPAddress.TryParse(proxy, out var address)
                    ? address
                    : throw new InvalidOperationException($"{section}:KnownProxies: '{proxy}' is not an IP address."))
                .ToList();

            var networks = (builder.Configuration.GetSection($"{section}:KnownNetworks").Get<string[]>() ?? [])
                .Select(network => System.Net.IPNetwork.TryParse(network, out var range)
                    ? range
                    : throw new InvalidOperationException($"{section}:KnownNetworks: '{network}' is not a range like 10.0.0.0/8."))
                .ToList();

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // One proxy in front: only the last hop it adds is read.
                options.ForwardLimit = 1;
                // Only what the settings list; the built-in "trust localhost" default goes.
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
                proxies.ForEach(options.KnownProxies.Add);
                networks.ForEach(options.KnownIPNetworks.Add);
            });
        }
```

(d) In `ConfigureApp`, first line before `app.UseRouting();`:

```csharp
            // First: everything after it (logs, Duende's addresses, the rate limiter) must see the real client IP and scheme.
            app.UseForwardedHeaders();
```

(e) Settings, all five `applicationSettings*.json`, top level (empty locally; filled at deploy with the edge's IP or its
Docker network such as `"172.18.0.0/16"`):

```json
  "ForwardedHeadersSettings": {
    "KnownProxies": [],
    "KnownNetworks": []
  },
```

**Also at deploy.**
- `AllowedHosts` in each service's settings: Identity answers any `Host` header today, and Duende builds its addresses
  from it.
- Rate limiting per client IP on sign-in, forgot and reset only works once the real IP arrives (point 4 above).

**Tests at deploy.**
1. `curl -I https://<site>` shows `Strict-Transport-Security: max-age=31536000; includeSubDomains`.
2. `http://<site>` pages redirect to HTTPS; `http://<site>/api/...` answers an error, not a redirect.
3. Identity's discovery document lists `https://` addresses.
4. A request sent straight to Identity (not via the edge) with `X-Forwarded-Proto: https` gets `http://` addresses:
   the forged header is ignored.
5. The logs show real client IPs, not the edge's.

### Sources

- Microsoft Learn, Configure ASP.NET Core to work with proxy servers and load balancers (.NET 10):
  https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0
- Microsoft Learn, Enforce HTTPS in ASP.NET Core (HSTS):
  https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0
- NIST SP 800-207, Zero Trust Architecture: https://csrc.nist.gov/pubs/sp/800/207/final

---

## L3. DPoP: tokens that only work for the server that holds them

**Parked:** 2026-10-05, while building the web BFF client (change 1.2).

**Why.** Today an access token is a bearer token: whoever holds a copy can use it until it expires (15 minutes), and a stolen
refresh token works until it is used or expires. DPoP (RFC 9449) binds every token to a private key that never leaves the BFF.
A copy taken from a log, a proxy or a memory dump is useless without that key. RFC 9700 §2.2 says sender-constrained tokens
SHOULD be used, and FAPI 2.0 requires them.

```
 today:   thief copies token ──► calls the API ──► accepted
 DPoP:    thief copies token ──► calls the API without the BFF's key ──► refused
```

**Decided.** Wanted, but after the BFF works end to end. The browser never sees a token with the BFF, so the risk is lower
than before and the order is: BFF first, DPoP second.

**What it needs.**
1. Identity: `RequireDPoP = true` on the `harmony-web-bff` client (Duende 8 supports it; no migration).
2. BFF: a second key just for DPoP proofs; Duende.AccessTokenManagement signs a proof for every token request and API call.
3. Every API: check the DPoP proof next to the token (method, address, time, one use). This is a framework change in the
   JwtBearer setup; check the licence of the validation package we pick.
4. A shared replay cache for the one-use check (see L4).

**Waits for.** BFF steps 2 to 5 done.

**Tests.** A token sent with no proof is refused; a proof for another address or method is refused; a proof used twice is
refused; a token with a proof from another key is refused.

### Sources

- RFC 9449, OAuth 2.0 Demonstrating Proof of Possession (DPoP): https://www.rfc-editor.org/rfc/rfc9449
- RFC 9700, OAuth 2.0 Security Best Current Practice, §2.2: https://www.rfc-editor.org/rfc/rfc9700
- Duende IdentityServer, Proof-of-Possession (DPoP): https://docs.duendesoftware.com/identityserver/tokens/pop/

---

## L4. A shared replay cache when Identity runs on two or more servers

**Parked:** 2026-10-05, while building the web BFF client (change 1.2).

**Why.** Every signed proof the BFF sends (private_key_jwt) carries a one-use id (`jti`). Duende remembers used ids so the same
proof cannot be sent twice. Today it remembers them in memory, which is correct for one Identity server. With two servers behind
a load balancer, a copied proof refused by server A could still be accepted by server B.

```
 one server:   proof ──► A (remembers jti) ──► same proof again ──► A: refused
 two servers:  proof ──► A (remembers jti) ──► same proof again ──► B: never saw it ──► accepted
```

Everything else Duende keeps is already shared: pushed sign-in requests and refresh tokens in SQL, Data Protection keys in SQL,
signing keys from files.

**Decided.** When we run a second Identity server, the replay cache moves to Redis.

**What it needs.**
1. Register an `IDistributedCache` backed by Redis (Microsoft.Extensions.Caching.StackExchangeRedis), with the identity Redis
   user and its own key prefix. Duende's replay cache uses it automatically.
2. The Redis ACL for the identity user allows that prefix.
3. The same cache serves DPoP's one-use check (L3).

**Waits for.** A second Identity instance (scale-out or high availability).

**Test.** Start two Identity servers on the same database and Redis. Send one proof to the first: accepted. Send the same proof
to the second: `invalid_client`.

### Sources

- Duende IdentityServer, Distributed caching and the replay cache: https://docs.duendesoftware.com/identityserver/deployment/
- RFC 7523 §3 (one-use `jti`): https://www.rfc-editor.org/rfc/rfc7523
