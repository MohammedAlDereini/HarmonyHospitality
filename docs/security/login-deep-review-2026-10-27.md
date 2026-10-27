# Login deep review: company login, Duende, BFF, YARP (27 October 2026)

Scope: the company login (Microsoft, Google), Duende IdentityServer's settings, and the web BFF (OIDC client, YARP,
cookies, CSRF, refresh, sign-out). Checked line by line against the current official guidance named at the end. Nothing
was changed in the code. This file is untracked, for the owner's review.

Proof used: code reading, the s61 suite (company login against the local stand-in), and one probe run today on a
throwaway database (finding H2). Real Microsoft and Google were not used: the app registrations do not exist yet.

## Summary

```
Already at standard        most of it (list below)
High                       1   sign-in session ended at 60 min (fixed); licence was a mistake, now a note
Medium                     8   company login hardening, two old BFF items
Low                        5   later or small
Owner-only portal steps    6
Decisions for the owner    3
```

## Fixed on 27 October (uncommitted)

```
H2  CoordinateClientLifetimesWithUserSession + ExpiredSessionsTriggerBackchannelLogout   probe: a refresh now moves
    the session (Expires 13:02 -> 13:03)
M1  confirm-with-company needs auth_time, made after the confirmation began (1 min leeway)  s61 E3: no auth_time and a
    10-minute-old one refused, a fresh one accepted
M2  Microsoft e-mail verified only when xms_edov is true                                    s61 E3: xms_edov false links nobody
M3  two-step recorded only when Microsoft's amr says mfa (session amr external,mfa)         s61 E3: Rami external,mfa|1,
    Noor external|0; Google never counts
M7  BFF and web app return paths: no "//", "/\", backslash, colon or control characters;   s40: "/\evil.example" and
    the ?lang= redirect never leaves the site                                               "//evil.example" land at "/";
                                                                                            s54: "//evil.example?lang=ar"
                                                                                            comes home; web unit test
M8  a refresh waiter that never took the lock never releases it                             s41 25/25 (eight parallel
                                                                                            calls, one refresh)
```

Suites after the fixes: s61 14/14, s40 24/24, s41 25/25, s54 23/23, web unit tests 217/217. The full regression runs
once at the end of the stages.

**Until the owner adds the optional claims on the Microsoft app** (`xms_edov`, `amr`, `auth_time`): a FIRST Microsoft
sign-in is refused (no `xms_edov`), "confirm it is you" with Microsoft is refused (no `auth_time`), and nobody is recorded
as two-step. People already linked keep signing in (their link is Microsoft's oid). This is the safe side on purpose.
Google is unaffected (it sends `email_verified`, and `auth_time` because `max_age` is asked).

## Already at standard (kept as is)

**BFF (RFC 10017 browser-based apps, RFC 9700 OAuth security BCP)**
- Confidential client with private_key_jwt (EC P-256), PKCE S256, PAR required (fails closed), strict client-assertion audience.
- Tokens never reach the browser. The cookie is `__Host-`, HttpOnly, Secure, SameSite=Strict, and carries only a key; the session lives in Redis.
- Idle and absolute caps, a custom-header CSRF wall plus an Origin check, deny-by-default authorization.
- Back-channel logout checked the way the spec asks: signature, issuer, audience, `logout+jwt`, events claim, no nonce, jti replay.
- Refresh: one-time tokens, the new id_token is validated and must name the same subject; the refresh token is revoked at sign-out.
- Clear-Site-Data at sign-out (never "cookies"), the caller's Authorization header and `Framework-` headers stripped, X-Forwarded-For set and never appended.

**Company login (Microsoft and Google guidance)**
- Microsoft multi-tenant tokens are validated exactly as Microsoft's own steps say: `tid` is a GUID, `iss` is `{instance}{tid}/v2.0`, and the signing key's issuer matches.
- The customer is pinned by that issuer (its tenant id). The permanent id is Microsoft `oid` or Google `sub`, never the e-mail.
- Google: the `hd` claim and `email_verified` are checked in the token, as Google asks.
- PKCE to both providers. A separate callback path per provider is RFC 9700's mix-up defence (4.4.2.2).
- The external cookie is dropped after one step. No provider tokens are kept. The sign-in page's CSP form-action names the provider hosts. The callback is rate-limited.

**Duende**
- 15-minute access tokens. One-time refresh tokens, sliding 60 minutes and absolute 12 hours.
- Server-side sessions, back-channel logout to the BFF, check-session iframe off, Lax sign-in cookie.
- Data-protection keys encrypted at rest with a certificate.

## High

### H1. Licence (corrected: not a problem, one note)
- **Corrected 27 Oct** after reading Duende's licence agreement (effective 15 Oct 2026) and the owner's approval e-mail.
- **Production is covered.** Community Edition is free for production when the company is under $1M revenue and $3M capital and Duende approves it in writing (section 4.A). The owner's key is that approval, serial 12386, valid 2 Oct 2026 to 2 Oct 2027.
- **The one note:** section 2.B(iv)(e) needs Duende's prior written approval for services offered to third parties that are based in part on the software. Harmony is sold to hotels.
  - If the Community application described Harmony that way, the approval covers it.
  - Otherwise, one written question to Duende closes it.
- **Who:** owner. Keep the approval e-mail, and Duende's answer if one is asked for.

### H2. Duende's sign-in session ends 60 minutes after sign-in, even while the person is working (proven)
- **What:** the probe signed in, waited 65 seconds and refreshed. The refresh answered 200, and the ServerSideSessions row did not move: Renewed stayed 11:47:22 and Expires stayed 12:47:22. Duende extends that session on refresh only when `CoordinateClientLifetimesWithUserSession` is on (Duende docs, "Inactivity timeout"). We never set it.
- **Effects:**
  - At minute 60 Duende's cleanup deletes the session row. The person keeps working through the BFF, because refresh tokens run 12 hours.
  - Stage B's closing job then marks their OperatorSessions record "expired" while they are still active. This is my stage B bug.
  - The device list stops showing that browser. "Sign out this device" may no longer reach it: Duende still revokes refresh tokens by session id, but the BFF gets no back-channel call. Not verified; the BFF should still lose the session within 15 minutes, when the access token runs out.
  - Expired sessions never tell the BFF: `ExpiredSessionsTriggerBackchannelLogout` is off by default.
- **Fix:** in `AddIdentityServerHost`, add these two settings, then test that an active session survives 60 minutes and an idle one ends at 60 with a back-channel call:
  - `options.Authentication.CoordinateClientLifetimesWithUserSession = true;`
  - `options.ServerSideSessions.ExpiredSessionsTriggerBackchannelLogout = true;`

  Both names are present in the Duende 8.0.10 DLL.
- **Where:** `Harmony.Identity.Infrastructure/DI/DependenciesConfigurator.cs`, around line 471.

## Medium

### M1. "Confirm it is you" with the company can pass without a fresh password
- **What:** `ConfirmWithCompany.cshtml.cs` line 134 uses `auth_time`, and falls back to `iat` when it is missing. Microsoft does not send `auth_time` unless it is asked for. `iat` only says the token was minted now. Someone at an unlocked PC can remove `prompt=login` from the URL before it reaches Microsoft, get a fresh token from the open Microsoft session, and pass.
- **Fix:**
  - Owner: add `auth_time` as an optional ID-token claim on the Entra app. Google sends it when `max_age` is asked, which it is.
  - Code: refuse when `auth_time` is missing, and require it to be later than the moment the confirmation started.

### M2. Microsoft e-mail "verified" means "present"
- **What:** `CompanySignIn.cshtml.cs` lines 82–84 treats any Microsoft e-mail as verified. That leans on Microsoft's default of dropping unverified e-mails, and the default has exceptions (apps with earlier unverified sign-ins). Microsoft: "never use the email claim for authorization".
- **Our use:** the e-mail is used once, to link the first sign-in inside the pinned company on a listed domain. That makes the risk small, but the check should say what it means.
- **Fix:**
  - Owner: add the `xms_edov` optional claim and set `removeUnverifiedEmailClaim = true` on the app (one Graph PATCH).
  - Code: require `xms_edov == true` at the first link.

### M3. A company sign-in is recorded as two-step without proof
- **What:** the session gets `amr: external`. Stage B records MultiFactorSatisfied = true for every company sign-in, but we never see whether the company asked for two-step.
- **Fix:**
  - Owner: add the `amr` optional ID-token claim on the Entra app.
  - Code: record two-step only when `amr` contains `mfa`. Google gives no `amr`, so a Google sign-in stays "company, two-step not proven".
- **Decision D1:** should administrators who sign in with a company be required to prove two-step? Options: the company's Conditional Access "authentication context" (needs Entra P1 on the customer's side), or our own second step.

### M4. Shared front-desk PCs: the company session outlives Harmony's sign-out
- **What:** signing out of Harmony leaves the Microsoft or Google session open in that browser. The next person who types the previous person's e-mail, or gets it from autofill, is signed in with no password.
- **Fix options (decision D2):**
  - `prompt=select_account` on every company sign-in. The minimum.
  - A per-customer "shared desks" switch that sends `prompt=login`.
  - "Also sign out of Microsoft" at Harmony sign-out.

### M5. Microsoft app credential is a client secret
- **What:** Microsoft: use a certificate (or a federated credential) in production. A secret lives at most 24 months, and Microsoft recommends under 12.
- **Fix:** sign a client assertion with a certificate, the way the BFF already signs its own assertion to Duende. Google only offers a secret, so rotate it yearly.
- **Who:** owner makes the certificate; code change is small.

### M6. App instance property lock on the multi-tenant app
- **What:** Microsoft says multi-tenant apps should lock every sensitive property of their service principals. Otherwise a customer's admin can add credentials to Harmony's app instance in their tenant.
- **Fix:** owner turns on the lock for all properties, under App registrations → Authentication. It has been on by default for new apps since June 2026; confirm it on ours.

### M7. The two BFF open redirects from the 22 Oct audit are still there
- `?lang=` redirects to the raw request path, so `//evil.example?lang=ar` goes off-site (`Program.cs` lines 257–262).
- `LocalPathOnly` accepts `/\evil.example`, which browsers read as `//evil.example` (`Program.cs` lines 522–525).
- **Fix:** small. Reject `/\`, `//` and control characters, the way ASP.NET's `IsLocalUrl` does, and rebuild the language redirect from a normalised path.

### M8. The refresh lock can be released by a request that never held it (22 Oct audit A06-001)
- **What:** `CookieOidcRefresher.cs` lines 85–93: a waiter that gave up still calls Unlock. Under load, two requests can then spend the same one-time refresh token, and the person's session ends.
- **Fix:** unlock only when the lock was taken. Alternatively, adopt `Duende.AccessTokenManagement`, Microsoft's documented alternative to this refresher; it is a new package, so it needs the owner's yes.

## Low

- **L1. One audience for every service.** RFC 9700 2.3 recommends tokens restricted to one resource server. Fine while Identity is the only API. When the hotel services arrive, give each its own audience with resource indicators (RFC 8707), which Duende supports.
- **L2. Access tokens are not sender-constrained.** This is a SHOULD in RFC 9700 and a MUST in FAPI 2.0. The tokens live only on the BFF, so the risk is low. DPoP is in our Duende key for later.
- **L3. `/bff/login` has no rate limit.** Every anonymous hit pushes a PAR request to Identity. Give it the same per-address limiter as the other doors.
- **L4. Packages.**
  - ASP.NET Core 10.0.14 is out; we use 10.0.13. Read its notes before production.
  - Duende 8.0.10 and YARP 2.3.0 are the latest.
- **L5. Guest accounts are refused.** We use Microsoft's `organizations` endpoint, so a guest in the customer's tenant signs in from their home tenant and is refused (wrong tid). That is consistent with the design. The `acct` claim would let guests in later, if wanted.

## Owner-only steps (Azure and Google portals)

1. Keep Duende's Community approval e-mail. If Duende was not told Harmony is sold to hotels, ask them once in writing (H1).
2. On Harmony's Entra app, add the optional ID-token claims `auth_time`, `xms_edov` and `amr` (M1, M2, M3).
3. On the same app, set `authenticationBehaviors.removeUnverifiedEmailClaim = true` (M2).
4. On the same app, turn on the app instance property lock for all properties (M6).
5. Add a certificate credential and retire the secret (M5).
6. Rotate the Google client secret yearly, and verify the publisher on the Microsoft app, so admins of other companies see a verified name at consent.

## Decisions for the owner

- **D1:** must administrators who sign in with a company prove two-step (M3)?
- **D2:** shared front-desk PCs: account picker always, a per-customer "always ask for the password" switch, or also sign out of the company (M4)?
- **D3:** when to give each hotel service its own token audience (L1): now, or with the first service?

## Not covered here

- The Angular app.
- The services' token validation (covered by the 12 and 22 Oct audits).
- Production infrastructure: TLS, proxies, Redis and SQL settings (server day, `later.md`).
- Real Microsoft and Google tenants.
- Stages B to D themselves, beyond H2.

## Sources

- Microsoft: [Validate tokens (multi-tenant issuer and signing-key issuer)](https://learn.microsoft.com/entra/identity-platform/access-tokens#validate-tokens)
- Microsoft: [Optional claims reference (auth_time, xms_edov, acct, amr)](https://learn.microsoft.com/entra/identity-platform/optional-claims-reference)
- Microsoft: [Prevent email claims with unverified domain owners](https://learn.microsoft.com/graph/applications-authenticationbehaviors)
- Microsoft: [Security best practices for app registration (credentials, app instance lock)](https://learn.microsoft.com/entra/identity-platform/security-best-practices-for-app-registration)
- Microsoft: [Add and manage application credentials](https://learn.microsoft.com/entra/identity-platform/how-to-add-credentials)
- Microsoft: [Conditional Access authentication context](https://learn.microsoft.com/entra/identity-platform/developer-guide-conditional-access-authentication-context)
- Microsoft: [Blazor Web App with OIDC, BFF with YARP, CookieOidcRefresher and the Duende alternative](https://learn.microsoft.com/aspnet/core/blazor/security/blazor-web-app-with-oidc?view=aspnetcore-10.0)
- Duende: [Server-side sessions: inactivity timeout](https://docs.duendesoftware.com/identityserver/ui/server-side-sessions/inactivity-timeout/), [session expiration](https://docs.duendesoftware.com/identityserver/ui/server-side-sessions/session-expiration/)
- IETF: [RFC 9700 OAuth 2.0 Security BCP](https://www.rfc-editor.org/rfc/rfc9700.html), [RFC 10017 OAuth 2.0 for Browser-Based Applications](https://www.rfc-editor.org/rfc/rfc10017.html)
- Google: [Verify the Google ID token on your server side](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token)
