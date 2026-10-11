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

---

## L5. EPOS: staff switch on the till with a PIN or a card

**Parked:** 2026-10-06, after the two-step exception list (2.3b).

**Why.** Harmony will have its own EPOS next to the PMS, like Mews (Mews POS) and Oracle (Simphony with OPERA). At a till many
people share one device and switch many times a shift. A full password and two-step at every switch is too slow, so both
vendors use a short unlock on a device that was trusted first.

```
 back office (web):  e-mail + password + two-step                                   (built)
 the till:           a manager signs the terminal in once ──► staff switch with a PIN or a card
```

**What Mews and Oracle do.**
- Mews POS: a 4-digit PIN for cashier roles. It unlocks the app only after the person signed in with the password on that
  device; an admin can "sign in all cashiers at once".
- Oracle Simphony: an employee ID by magnetic card, RFID card or keypad; a fingerprint alone or with the ID. Oracle recommends
  ID + fingerprint, and at least 6 digits when IDs are typed.

**Decided.** Nothing yet. The back office keeps two-step.

**What it needs (first sketch).**
1. Registered terminals: a manager signs a till in once; the terminal has its own credential and can be removed remotely.
2. A till PIN (or card id) per person, stored only as a hash, with lockout; never accepted outside a registered terminal.
3. Short till sessions per person, so every sale says who made it.

**Waits for.** The EPOS product design.

### Sources

- Mews Help, Setting up user PIN in Mews POS: https://help.mews.com/s/article/setting-up-user-pin?language=en_US
- Oracle Simphony, POS Client User Authentication:
  https://docs.oracle.com/en/industries/food-beverage/simphony/simsg/pos_client_user_authentication.htm

---

## L6. EPOS payments: our own terminals, a payment processor behind them

**Parked:** 2026-10-06, after looking at how Mews earns on payments.

**Why.** At Mews, payments are the business. 2025, from Mews's statutory filings (Skift, 2026-10-06):

| Item | Amount |
|---|---|
| Total revenue | €291M |
| From payments | €219M (75%) |
| From subscriptions | €62M |

They moved €17B of payments and paid €172M to payment gateways ("including interchange and bank fees"), so roughly €47M
(about 0.28% of the volume) stayed with Mews. Even so, the 2025 operating loss was €111M.

**How Mews does it.**

```
 Mews POS / PMS ──amount──► Mews Terminal ──encrypted card──► Adyen or Stripe (process it)
                                 │
                                 └──► payment posted to the right bill or booking, automatically
```

- Own-branded terminals (A1 to A4, S2). Adyen or Stripe is the "Terminal Provider", and the hotel accepts that provider's terms.
- One stored card (a token) works at the front desk, the kiosk and in F&B. Card details stay with the payment providers, never
  in Mews.
- Each payment pays a Mews Payments fee ("the commission paid to Mews") + interchange + scheme fees, plus a fee for stored cards.
  No public prices; every hotel gets a quote.
- One real contract, from a payments consultant (not Mews): Visa/Mastercard interchange + 1.0%, Amex 3.7% flat, $0.55 per
  stored card, $15 per chargeback, $0.10 terminal fee.

**How Mews gets its cut: the processor splits every payment.**

```
 guest taps the card ──► Adyen ──► guest's bank says yes
                           │
                           ▼  the money arrives at Adyen
 €100 split (example numbers):
   €0.50  → guest's bank + Visa
   €1.00  → Mews (its commission)
   €98.50 → paid out to the hotel
```

- Mews never bills the hotel for payments: its cut comes out before the payout. Adyen's platform product does this split
  (sale amount, platform commission, card fees) on every payment.
- Mews does not make terminals. It sells the provider's terminals under the Mews name, under a contract with Adyen or Stripe.
- The cut is not only from terminals: online card payments (stored cards, payment links) go through the same Mews Payments.
- Mews's own contract with Adyen and Stripe is private; the split mechanism is Adyen's public product, not a Mews disclosure.

**Decide first (business).**

| Option | We earn | We carry |
|---|---|---|
| Payment facilitator like Mews (Adyen for Platforms or Stripe Connect) | a fee on every payment | onboarding checks, chargebacks, compliance |
| Connect to the hotel's own processor and terminals | nothing on payments | little |

Either way, card numbers never touch Harmony: terminals encrypt to the processor and Harmony keeps only tokens.

**Waits for.** The EPOS product design and the business decision above.

### Sources

- Skift, Inside Mews' $2.5 Billion Valuation (2026-10-06):
  https://skift.com/2026/10/06/inside-mews-2-5-billion-valuation-can-the-economics-catch-up/
- Mews, Payment Terminal Policy (Adyen / Stripe): https://www.mews.com/en/legal/terminal-terms
- Mews, Hotel Payment Terminals: https://www.mews.com/en/products/terminals
- Mews Help, Mews Payments fees: https://help.mews.com/s/article/merchant-fees?language=en_US
- Mews Help, What is Mews Payments: https://help.mews.com/s/article/what-is-mews-payments-and-how-does-it-work?language=en_US
- Adyen, Split transactions between balance accounts (platform commission): https://docs.adyen.com/marketplaces/split-transactions
- Merchant Cost Consulting, Mews payment fees breakdown:
  https://merchantcostconsulting.com/lower-credit-card-processing-fees/mews-hotel-payment-processing-fees-breakdown/

---

## L7. Leaked-password check: a paid supplier if a customer asks

**Parked:** 2026-10-07, after the password blocklist (3b).

**Why.** 3b checks every new password against Have I Been Pwned (HIBP): free, no account, named by OWASP, and the UK NCSC
and the FBI work with it. Only 5 characters of the password's SHA-1 leave the server. Banks buy the same check from paid
suppliers instead, for fresher data (dark web, password-stealing malware) and a contract. A large hotel group may ask for
the same.

```
 now:    new password ──► LeakedPasswordValidator ──► HIBP (free)
 later:  new password ──► LeakedPasswordValidator ──► a paid supplier
         (one class and its settings change; nothing else)
```

**The suppliers (prices checked 2026-10-07).**

| Supplier | What it is | Cost |
|---|---|---|
| Google reCAPTCHA "password defense" | checks a password against leaks | 10,000 a month free, then $8 flat to 100,000, then $1 per 1,000 |
| Enzoic | password-check API | 2,000 calls free, then $0.04 down to $0.008 a call |
| SpyCloud | enterprise; dark-web and malware data | not public, quote only |
| AWS WAF Account Takeover Prevention | guards the sign-in page, not password changes | $10 a month + $1 per 1,000 sign-ins; the app must sit behind AWS WAF |
| HIBP offline | the whole list on our own server, no outside call | free; tens of GB, refreshed by hand |

Microsoft has no public service for this: its checks cover only passwords stored in Microsoft Entra (hotels that sign in
with SSO).

**Decided.** Keep HIBP. Change supplier only when a customer asks for it.

**Waits for.** A customer asking.

### Sources

- OWASP Authentication Cheat Sheet (Pwned Passwords): https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html
- Have I Been Pwned, Pwned Passwords: https://haveibeenpwned.com/Passwords
- Google reCAPTCHA pricing: https://cloud.google.com/security/products/recaptcha#pricing
- Enzoic APIs: https://www.enzoic.com/apis/
- SpyCloud, financial services: https://spycloud.com/industry/financial-services/
- AWS WAF Fraud Control pricing: https://aws.amazon.com/about-aws/whats-new/2023/06/aws-waf-fraud-control-account-creation-prevention-pricing
- Microsoft Entra Password Protection: https://learn.microsoft.com/en-us/entra/identity/authentication/concept-password-ban-bad

---

## L8. 15+ characters for people on the two-step exception list

**Parked:** 2026-10-07, step 3c, skipped on purpose.

**Why it came up.** People on the exception list (2.3b) sign in with a password alone. NIST SP 800-63B-4 asks 15+ characters
for a password used alone (8 with two-step). Everyone else keeps 12 + a symbol.

**Why it waits.** Mews keeps one rule for everyone (12 + a symbol) and guards exceptions with its security score instead. The
exempt door is already small: never a Manager or higher, an alert e-mail when someone is put on the list, and leaked
passwords are refused (3b).

**What it needs (sketch).** One more password check (exempt and two-step off: 15+), a new code, and at sign-in "must change
the password" when an exempt person's password is shorter. No migration.

**Waits for.** A customer or an audit that asks for NIST's single-factor rule.

---

## L9. Leaked-password check at sign-in

**Parked:** 2026-10-07, step 3d.

**Why.** 3b checks a password when it is set. A password that was fine then can appear in a leak later. NIST SP 800-63B-4 asks
to force a change when a password is known to be compromised.

**What it needs (sketch).** At sign-in, the password is known for a moment: check it against Have I Been Pwned the same way
(5 characters of the SHA-1), and when it is listed, sign the person in with "must change the password". If the service
cannot answer, sign in as usual.

**Waits for.** After item 4 (audit log), so the forced change is recorded.

---

## L10. Rate limits per property network

**Parked:** 2026-10-07, item 5 (rate limits).

**Why.** Item 5 counts tries per caller address. A hotel's staff share one public address, often with the guest Wi-Fi, and a
head-office VPN or a remote desktop can put hundreds of staff behind one address. One abuser on that network can use up the
sign-in budget for everyone there for a few minutes, and a big office may need more than 60 at once + 20 a minute.

**What it needs (sketch).** Each property lists its own network ranges. Callers from a listed range get that property's own,
higher budget; everyone else keeps the global one. Floods from many addresses are for bot protection at the front door (L2).

**Waits for.** The Tenant ticket (properties live there) and L2.

---

## L11. One count across two or more servers

**Parked:** 2026-10-07, item 5.

**Why.** The counts live in each server's memory. With two or more Identity servers behind a front door that spreads the
calls, a caller gets one budget per server.

**What it needs (sketch).** The counters in Redis, which we already run: one increment with an expiry per try, our own few
lines through the framework's Redis, no new package. Or a front door that keeps a caller on one server.

**Waits for.** A second Identity server, together with L4.

---

## L12. One sign-in e-mail a minute per account

**Parked:** 2026-10-07, found during item 5.

**Why.** Every right password at the sign-in page sends a new e-mail: the two-step setup link, or the e-mail sign-in link.
Forgot password already sends at most one mail a minute per account; these two do not. Abuse needs the password, but a
double click already sends two.

**What it needs (sketch).** The same per-account rule as forgot password (a cache key for one minute); the page then says the
link was already sent.

**Waits for.** Nothing; it is small.

---

## L13. At most 100 wrong tries in a row on one account (NIST)

**Parked:** 2026-10-07, item 5.

**Why.** NIST SP 800-63B-4 §3.2.2 says a verifier SHALL allow no more than 100 failed tries in a row on one account. Our
lockout (5 wrong → 5 minutes) starts counting again after each lock, so guessing never stops; it is about 60 tries an hour.
That meets OWASP ASVS 2.2.1 (at most 100 an hour) but not NIST's "in a row". Mews publishes no such rule; it blocks risky
sign-ins and its security team checks them by hand.

**What it needs (sketch).** Count wrong tries across locks (one column, a migration). At 100 the account stays locked until a
password reset or an admin unlocks it, with an alert e-mail and a trail row.

**Waits for.** An audit that asks for NIST, or before go-live.

**Sources**
- NIST SP 800-63B-4, §3.2.2 Rate limiting: https://pages.nist.gov/800-63-4/sp800-63b.html
- OWASP ASVS 4.0.3, V2.2.1: https://github.com/OWASP/ASVS/blob/v4.0.3/4.0/en/0x11-V2-Authentication.md
- Mews, login blocked as account takeover: https://help.mews.com/s/article/How-to-log-in-to-Mews-when-login-displays-Error-503-page

---

## L14. Forgot password answers in the same time

**Parked:** 2026-10-07, found during item 5.

**Why.** OWASP asks a forgot-password answer to take the same time whether the account exists or not. Ours sends the mail
before it answers, so a real account answers more slowly and accounts can be found by timing. Item 5's limit slows that
down; it does not stop it.

**What it needs (sketch).** Answer first and send after (a background mail queue), the same way for every address.

**Waits for.** A background mail queue (outbox).

**Sources**
- OWASP Forgot Password Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html

---

## L15. Remove the token door with BFF step 5

**Done:** 2026-10-11, BFF step 5. `TokenDoorLimiter` went with the password and `mfa_otp` grants, the `harmony-web`
client and CORS; the sign-in pages keep `[EnableRateLimiting]`, and only the BFF (code + refresh) calls the token door.

---

## L16. Trusted devices and approving new sign-ins

**Parked:** 2026-10-08, after item 5.

**Why.** Item 4 only tells: a sign-in from an unknown browser sends the person an e-mail, and the new browser is already in.
The wish is to ask first: the person marks a device as trusted, and a sign-in from any other device waits until they
approve it there (Google's "Is it you?", WhatsApp's new device). Yes lets the new browser in; No refuses it.

**What was learned (sketch, not decided).**
- Plain Yes/No is not safe any more: attackers send prompts until a tired person taps Yes (the 2022 Uber breach).
  Microsoft made number matching mandatory in 2023: the new browser shows a number, the person types it on the
  trusted device.
- A fallback is needed when nobody can approve (one front-desk PC, the trusted device switched off): for example the
  normal two-step code.
- Storage: "trust this device" is one more column on `UserDevices` (no new table); the waiting approval lives a few
  minutes, so a cache entry, not a table.
- Check Mews's own trusted-device behaviour in its docs before designing, and decide who may trust a device (the person,
  or an admin) and for how long.
- CONFIRMED 2026-10-09 from Mews's own docs ("Securing your Mews Operations user accounts"): a first sign-in from a new
  device or location is approved through an e-mailed link; the device list lives in account settings and removing one
  signs it out remotely; the whole feature is off when SSO is enforced. Our design matches.

**Waits for.** The rank rules (C1 + C2) first; then a design pass of its own.

---

## L17. R4 (last administrator, 00079) on the account doors when cross-tenant support arrives

**Parked:** 2026-10-09, with the rank rules.

**Why.** Today R4 lives only on the role doors (Role/Update, Role/Delete), because through the account doors the case
cannot arise: whoever may suspend, terminate or demote an administrator (R2: equal or higher rank) is an active
administrator themselves, so one always remains. The one reachable hole was the sole admin lowering or deleting the
role they stand on — closed with `SoleAdministratorRoleAsync`.

**What changes it.** The day a platform (super) admin can reach INTO a hotel tenant (cross-tenant support tooling),
that caller is not counted in the tenant's own administrators, so suspending or demoting the tenant's last admin
becomes reachable. Add the 00079 check to Suspend, Terminate and UpdateRoles in the same ticket that opens
cross-tenant access.

**Waits for.** The tenant ticket (00070–00073) / platform support access.

---

## L18. Step-up re-authorization for the dangerous moments

**Parked:** 2026-10-09, from the Mews deep-dive.

**Why.** Mews forces a person to verify their identity AGAIN — a live session is not enough — when they add a user,
download a report with sensitive data, or open areas holding sensitive data ("Securing your Mews Operations user
accounts"). It is why their all-powerful Admin is survivable: a stolen admin session still cannot quietly add an
account or pull the guest list.

**Sketch.** A short-lived "recently proved" marker (cache, like the MFA done-mark): the guarded endpoints refuse with
their own error code unless the caller re-entered their password or code in the last N minutes; the web app catches
that code and shows the prove-yourself dialog. Guard first: Create user, UpdateRoles, role doors, sensitive exports.

**Waits for.** BFF steps 2–5 (the dialog belongs in the cookie world, not the token door).

---

## L19. Graded enforcement: mask sensitive data instead of refusing

**Parked:** 2026-10-09, from the Mews deep-dive.

**Why.** Since 2024-08-07 Mews lets accounts without two-step still sign in, but masks every customer e-mail and phone
for them — in screens AND exports — including two-step-exempt accounts. The kiosk can check guests in but can never
leak the guest list. This composes perfectly with our exception list (L-free today: exempt accounts have full sight).

**Sketch.** One flag on the caller's identity ("reduced sight": no two-step proved on this session), honoured by the
queries that return personal data. Decide the masked fields per module when the PMS modules exist.

**Waits for.** The first module that serves guest personal data.

---

## L20. Tenant ticket design input (the Mews/Opera multi-property truth, 2026-10-09)

Collected for the tenant ticket (00070–00073); evidence in the session notes.

- Both vendors: the VENDOR builds the top container (Oracle provisions chains/properties; Mews builds the portfolio at
  onboarding). Ours: the platform super admin creates tenants — already our model.
- Both vendors: every property/tenant is BORN with its admin (Opera seeds `<CODE>-ADMIN` groups; Mews's first
  onboarded user is the Admin). Ours: on tenant creation, seed one system role "Administrator" (level Administrator,
  all permissions, Mews-style) and invite the first admin with it. One seed + one invitation — no hand-made admins.
- The in-between cases (see a few properties, GM of some, finance across many) are NEVER solved with half-admins at
  the top: both vendors compose them from below with per-property MEMBERSHIP + a role per membership (Opera's hubs =
  key 2). Properties inside a tenant should follow the same two-keys shape when they arrive.
- Group-of-properties admin (chain of groups) exists only in Opera (enterprise → chain → property, an admin group per
  node, each scoped). Mews has two floors only, and its portfolio admins are all-or-nothing. If Harmony ever needs a
  middle floor, Opera is the model; Mews proves it can wait.
- Nice-to-have seen at Mews: a security score (Low→Very high) driven by the SHARE of admins (6+ in a small property or
  >30% = Low), exceptions count, passkeys and SSO — a cheap, honest admin-dashboard idea for much later.

---

## L21. The trail's storage ladder (when the table grows heavy)

**Parked:** 2026-10-09, with G1. The trail stays ONE table with the Action column (the Entra/Windows/Opera layout:
same shape = same table, the event type is a value; a new table only for a new SHAPE, as UserDevices already is).
G1's sweep caps it at 12 months of rows. When that is still too heavy, climb one rung at a time:

1. **Month drawers** — SQL Server table partitioning by OccurredAtUtc (every edition since 2016 SP1). One code-first
   migration carrying the raw SQL (partition function + scheme + clustered index on (OccurredAtUtc, Id)); the sweep
   swaps batched DELETE for SPLIT + TRUNCATE of whole partitions. The app never notices. Trap: every index must carry
   the date column.
2. **Tenant shards** — a database per tenant (or group), the whole service's scaling rung, decided in the tenant era;
   the tenant filter already hides tenancy from the handlers, which is what makes this possible without rewriting them.
3. **Export** — the Microsoft way at Microsoft scale: Entra keeps sign-in logs 30 days and exports the rest (Log
   Analytics + storage with lifecycle rules). Ours: a short hot window in the DB, old months shipped to cheap files.
   The card rules bless it: 3 months immediately available, 12 retained anywhere.

## L22. Session LOCK with a PIN for card-scope deployments (the PCI 8.2.8 answer)

**Parked:** 2026-10-10, with the BFF session clocks (decided 60 minutes idle / 12 hours absolute, NIST 800-63B
rev 4 AAL2). When payments land (the EPOS era, L6), front-desk machines enter card scope and PCI DSS 8.2.8
expects re-authentication after 15 idle minutes — the rule Opera answers with a hard 15-minute logout its users
hate, and the gap Mews users beg about on the feedback forum (no inactivity protection at all; a community
Chrome extension was the official workaround). The kind answer is the one Mews customers keep requesting:
a 15-minute session LOCK over the still-alive session, unlocked with a short PIN — PCI accepts a lock screen
as re-authentication, and the desk keeps its open folios. Build it with the till sign-in work (L5/L6): the lock
is a front-end overlay + a BFF check, the PIN is per-user, and the idle/absolute cookie clocks underneath stay
as they are (a card-strict deployment may also just set SessionIdleMinutes to 15 — the knob already exists).

## L23. After sign-out: a "signed out" page, and the sign-in method after a refresh

**Parked:** 2026-10-10, with BFF step 4. Two small things seen in the real-browser proof, neither wrong today:

1. Signing out lands on "/", the app finds no session and walks straight back to Identity's sign-in page — correct,
   but a person who meant to leave sees a sign-in form. The kind answer is a public "You are signed out" page
   (PostLogoutRedirectPath pointing at it, the app's router serving it without a session).
2. The app says "password + authenticator" from the id_token's amr claim. The refreshed id_token Identity hands the
   BFF on a silent refresh may not carry amr; then the wording falls back to "password" after the first refresh.
   Either keep the first id_token's amr in the session at sign-in, or ask Identity to repeat it on refresh.
