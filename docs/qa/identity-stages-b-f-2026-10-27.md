# Identity stages B to F: decisions and build ledger (27 October 2026)

The user said "do all this in one go" for stages B to F. These decisions are taken under that instruction, using the recommendations already given. Every stage is built, tested by its own suite, and then the whole regression runs once at the end. Nothing is committed until the user says so.

## Decisions taken

- **B, invitations:** each invitation becomes a LocalStaffEnrolments row.
  - It records the inviter, the local authority, a SHA-256 digest of the e-mail, a state (Pending, Redeemed, Cancelled) and ExpiresAtUtc 24 hours after sending.
  - UserAccountId is added to the design's columns, so the admin can resend or cancel.
  - Resending or cancelling changes the account's security stamp, so the old link dies. An expired or cancelled invitation can never set a first password.
- **B, sessions:** each sign-in writes an OperatorSessions row: started, ended, two-step done, and Duende's session id.
  - It ends on sign-out, on signing out one device, on "sign out everywhere" and on any security-version bump.
  - A job closes rows whose Duende session has gone, after an idle timeout or expiry.
  - Rows are kept as long as the security trail (12 months), then the retention job removes them.
  - A company sign-in counts as two-step done only when the company says so (Microsoft's amr names mfa); Google never says it. Changed on 27 Oct by the login review (docs/security/login-deep-review-2026-10-27.md, M3).
- **B, confirm it is you:** StepUpAuthorizations replaces the 5-minute Redis mark.
  - The expiry is set once and never extended.
  - Company people confirm by going back to Microsoft or Google with a forced sign-in (prompt=login, max_age=0).
- **C, revocation feed:** Identity writes the feed in the same transaction as the security-version bump.
  - It keeps a sequence per customer, plus snapshots and gap tombstones.
  - A job compacts entries older than 7 days into a snapshot and always leaves a tombstone.
  - Read doors are for the platform only. Services keep their fast Redis security-version check; a framework reader is a later ticket.
  - SecurityOperations names each change, and feed entries point at it.
- **D, approvals:** a customer switch, Tenant.ApprovalsRequired, set by its administrator. It is off by default, so today's behaviour does not change.
  - When it is on, these actions need a second person: giving an administrator-level role (by hand, or by giving a directory group such a role), exempting from two-step, switching company login off, and merging accounts.
  - The person asking must hold a live StepUpAuthorization. The checker is never the maker. A request is void if its target's security version moved.
- **E, directory sync (changed 27 Oct, see docs/qa/directory-sync-standards-2026-10-27.md):** SCIM 2.0 push from Microsoft Entra ID and Okta, one door, the way Mews and OPERA do it. Google: sign-in only now (G1), reading Google's directory later (G3).
  - Three tables (ProvisioningConnections, DirectoryGroups, DirectoryGroupMembers) and three columns (UserAccounts.ProvisioningConnectionId and DirectoryExternalId, Tenants.InactivityDays). The pull-only tables, the quarantine and MigrationAmbiguities are gone.
  - The directory proves itself with the connection's client id and secret: a one-hour token from Harmony's own token door (client credentials; not Duende, whose licence counts clients), or the long "clientId.secret" token (Okta, Mews). A new secret kills both at once.
  - The group is the gate (the Mews way): a pushed person has no access until a group the customer's administrator gave a role takes them in; then the sign-up mail goes out. Their roles follow their groups only.
  - active=false suspends at once; active=true brings them back with today's groups; DELETE terminates and frees the e-mail (RFC 7644 3.6). An e-mail of another customer is a 409 back to the directory.
  - Two Mews rules: the e-mail never changes through SCIM; directory people are read-only in Harmony until the platform removes the connection.
  - The inactivity rule (PCI DSS 8.2.6): a nightly job suspends accounts with no sign-in for the customer's number of days (90 unless fewer); directory people and the super role are left out.
- **F, plumbing (changed 27 Oct after checking the framework and Microsoft's guidance: "the smaller F" plus five fixes):**
  - The outbox is the framework's own table (Outbox.OutboxContainerEntries). Identity writes each event into it in the same save as the data (IdentityOutbox, in IdentityDbContext.SaveChangesAsync); the after-commit publishers are gone.
  - Framework fixes: RabbitMQ publisher confirms; the job sends in row order, one sender across servers (sp_getapplock), stops at the first failure and retries with a growing wait (10 s doubling, at most 5 minutes); after 10 tries a row is set aside, logged as an error and reported by an "Outbox" health check; sent rows stay Completed 7 days; the job reads every tenant (the context's tenant filter no longer applies to it); the table is created at startup.
  - The inbox is the framework's request table (ClientRequest) mapped as InboxMessages, written in the receiver's own save, keyed by a hash of receiver + message id, cleaned after 14 days. Identity consumes nothing yet: a test receiver proves it.
  - Request keys stay on the receipt gate's Redis store; later.md L1b item 6: Redis saved to disk (AOF) and noeviction.

## Notes from the build

- C: the feed line is written by the Identity database context itself, so no handler can forget it. New accounts start at security version 2 (their roles are set at creation); a new account has no tokens, so it writes no line.
- C: the compaction door takes KeepDays (0 to 30) so tests and the platform can compact now; the hourly job keeps 7 days.
- D: Rejected is added to the ERD's states (pending, approved, void). Requests also keep the change itself (Payload) and the account it is about (TargetUserAccountId), which the ERD did not list.
- D: an approval runs the stored change through its own handler, inside one transaction with the execution record. Every rule of that handler is checked again against the checker. A refusal rolls everything back and the request stays waiting.
- E: the request's database context reads the customer once, when it is made, so the SCIM door checks the directory's token in a scope of its own and sets the customer before anything else touches the database.
- E: directory sign-up invitations name the person as their own sponsor (LocalStaffEnrolments needs one; the directory sent them).
- E: not covered by s65: the nightly timer itself (the platform door runs the same round), a role across a hotel group (the same assignment code as one hotel), and Entra's Workload Identity Federation (later).
- F: the framework's health page builds its list of checks itself on every call, so the outbox check joins it through the framework's HealthCheckOptions (a plain AddHealthChecks().AddCheck is cleared).
- F: the framework turns a unique-key violation into a BusinessException "Conflict"; SCIM answers 409 for it (it answered 400 before).
- D: the gated changes need ManageUsers, the same permission as the approve door. Switching company login off is gated in code; the suite does not drive it (it needs a company login set up), s61 still covers switching it off with approvals off.

## Ledger

- [x] B1 invitations (s62)
- [x] B2 sessions (s62)
- [x] B3 confirm it is you (s62, s61 company confirm)
- [x] C revocation feed (s63 5/5: one line per bump in the same transaction, six parallel writes without holes, platform-only doors, compaction with snapshot and tombstone)
- [x] D approvals (s64 7/7: off by default, maker confirms first, checker never the maker, void when the account moved, merge rejected then approved, switching off approved too)
- [x] E directory sync (s65 25/25: the token door both ways, Entra's test connection, users created, taken over, filtered, renamed, e-mail change refused, groups as the gate with the sign-up mail, Okta's member removal, read-only, off and on, delete and the e-mail freed, a new secret, switched off, the platform's removal, the inactivity rule, a role at one hotel and its end when the group goes, an administrator group waiting for approval; s64 7/7 after QuarantineReleases went)
- [x] F plumbing (s66 7/7: the outbox table before the first save, the row in the same save with the message id, confirm-mode channels, a crash after the save delivers each event once, order kept through an outage, a poison row set aside after 10 tries with the rows behind it going on and the health check Degraded, Completed rows deleted after 7 days, the inbox by a test receiver 5/5; s60 5/5 on the new design; s65 25/25 with the SCIM duplicate-key 409)
- [x] screens for B to F (web: Users head links to Company directory, Invitations, Approvals and rules; the person page shows the sign-ins and, for a directory person, a note with no role or state actions; console: a Company directory panel on the tenant page with Remove. Arabic for every new text and error code 00129-00141, 11001-11002, 11101-11104. Checks 0, web tests 247/247, console 2/2, both apps build en and ar)
- [x] full regression (27 Oct, tag BF on api-s66): 40 suites, 965 checks, 0 failed (s66 7/7, s65 25/25, s64 7/7, s63 5/5, s62 5/5, s61 14/14, s60 5/5, the gate s56 44/44, every older suite green)
- [x] browser walk (local stack, en and ar, web and console): Users links; Approvals and rules (days 120 refused, 45 kept; approvals on, off waits after "Confirm it is you", the maker cannot approve, a second administrator approves); Company directory (open, the secret once, a pushed group given Front desk at Ritz Amman); Invitations (the directory person's link, Send again); person page (directory note, no role editor, sign-ins); console panel (Remove asks first, then None). Fixed during the walk: the web app had no "Confirm it is you" step, so every guarded change failed once approvals were on (a shared dialog in the shell now asks for the password and sends the change again; company people are told to ask a password administrator); times without a zone (every createdDate) were read as device time, now as UTC; the sign-in rows' attribute clashed with "How they sign in". Web tests 256/256.

## After B to F: telemetry and resilience (28 October, in the framework once, every service gets both)

Microsoft's own list for a .NET microservice (the Aspire service defaults) had two items Harmony lacked that need no server. Both built on the user's "do this now".

- **Traces and metrics (OpenTelemetry):** `Harmony.Core` registers them in `AddCore` when `TelemetrySettings:OtlpEndpoint` is set (every request in, every HTTP call and SQL command out, the runtime's counters; health probes left out; stamped with the AppInfo name). Nothing is registered without an endpoint, so the suites and a service without a collector pay nothing. A SQL span keeps the statement with `?` and `@name` placeholders, never a value. Logs stay with Serilog, which carries the trace id. Locally the Aspire dashboard (`infra/aspire-dashboard`, Microsoft's developer tool, bound to this PC, no sign-in) receives it on http://localhost:4317 and shows it on http://localhost:18888; the Development settings of Identity, the web BFF and the console BFF point there; the production settings leave it empty (later.md L1b item 8).
- **Retries and timeouts on outgoing calls:** `Microsoft.Extensions.Http.Resilience`'s standard handler on every HttpClient the factory hands out: 30 s in all, 10 s per attempt, three retries with a growing wait on 5xx, 408, 429 and connection failures, a circuit breaker, a concurrency limit. Writes (POST, PUT, PATCH, DELETE) are never retried: a retry of a write belongs to the caller holding its idempotency key. YARP's proxied call gets an explicit 30 s and never retries (by design; the browser holds the key). `Grpc.Net.ClientFactory` lifted to 2.84 (below 2.64 a gRPC client with the resilience defaults fails at runtime; Harmony has no gRPC client yet).
- **Packages (approved 28 Oct):** OpenTelemetry.Extensions.Hosting, .Instrumentation.AspNetCore, .Instrumentation.Http, .Instrumentation.Runtime, .Instrumentation.SqlClient, .Exporter.OpenTelemetryProtocol (1.20.1), Microsoft.Extensions.Http.Resilience 10.11.0, Grpc.Net.ClientFactory 2.84.0, the image mcr.microsoft.com/dotnet/aspire-dashboard:13.6.2.

Proof:
- [x] `Harmony.TestEnv\s67\resilience-check` 4/4 against a stand-in server: a GET answered 503 twice comes back 200 on the third try; a POST answered 503 is handed back once, never retried; a first answer of 11 s is cut at the 10 s attempt and the fast retry wins (12.3 s in all); every answer too slow is given up at 30.0 s (TimeoutRejectedException).
- [x] the local stack with the dashboard: one person-page call is one trace of 7 spans over 2 services, `WebBff GET /api/{**rest}` -> `HTTP GET 200` -> `Identity GET api/UserAccount/{id:guid}` -> four SQL SELECTs with their times (one at 772 ms, to look at); the metrics pages list ASP.NET Core, Kestrel, System.Net.Http and runtime meters with P50/P90/P99 per method and status.
- [x] framework tests: MediatR 98, Identity 15, BuildingBlocks 8 green; the two old UnitTest projects (Cache, FileStorage) fail before and after this change for want of an `applicationSettings.json` with a real Redis and blob store (not touched).
- [~] full regression with telemetry on (tag TEL): stopped by the user at 31 of 40 suites, every one green (s66 to s35); the one full regression runs once, after DPoP (L3) and trusted devices (L16), before the push

## After telemetry: DPoP (L3) and trusted devices (L16), 28 October

The last two of the four parked decisions, built on "close this all in one go". Both in `docs/later.md` under their items.

- **DPoP (L3):** Identity requires a proof from both BFF clients (`RequireDPoP`), every token carries `cnf.jkt`; the BFF signs an ES256 proof per token call and per proxied API call (`DPoPProofFactory`, `DPoPProofHandler`, the `dpop_jkt` on PAR, the nonce retry); the API side is our own `DPoPProofValidator` in `Harmony.Core.Identity` (about 150 lines; Duende's JwtBearer package is not confirmed under the Community Edition), wired into the framework's bearer handler, with the `WWW-Authenticate` answer naming `DPoP` and the algorithms. Keys: `secrets/web-bff-dpop-key.pem` and `secrets/console-bff-dpop-key.pem` for the local run (production: `/run/secrets/...`), tracked the way the local client keys are.
- **Trusted devices (L16):** `Tenants.TrustedDevicesRequired` (off by default, `PUT /api/UserAccount/TrustedDevices`), `UserDevices.TrustedAtUtc`, migration `20261028074800_TrustedDevices`; the hold before `SignInAsync` in every sign-in page (password, passkey, two-step, two-step setup, e-mail link, password change); `/account/newdevice` waits and finishes the sign-in, `/account/trustdevice?token=` spends the one-use link from "Harmony security: trust this device to sign in"; company sign-ins never held; Sign out device also distrusts. Web: the "New devices" switch under Approvals and rules, the Trusted mark on device rows (10 Arabic units added).
- **Test harness:** every suite now sends DPoP (`helpers.ps1`, `page-sign-in.ps1`: the proof on token calls and API calls, `dpop_jkt` on PAR, the newdevice turn, the straight-in path for a browser that still holds its Identity session, `PsForgetSession`); s68 and s69 added to the runner.

Proof:
- [x] `s68\tests\e2e\dpop-e2e.ps1` 14/14 (the token door: no proof refused, a proof per request, the refresh bound by the client key per RFC 9449 section 5; the API: a Bearer token with `cnf` refused, a wrong key, a wrong method, a wrong address, a stale proof, a replayed `jti`, a wrong `ath`, the header's wording)
- [x] the gate `s56` 44/44 through the BFF with DPoP on (api-s68)
- [x] `s69\tests\e2e\trusted-devices-e2e.ps1` 8/8 (off by default straight in; on: a new browser waits, the mail, the link trusts it and the sign-in finishes, one held and one trusted; the same browser again straight in; the link once; a made-up token refused; the administrator's sign-out distrusts and the browser waits again; off again straight in; ViewUsers alone cannot touch the switch)
- [x] web: `pnpm run check` clean (1897 units, 0 boundary violations, 0 style problems), `ng test` 257/257, en and ar builds 420 kB initial
- [x] framework: Identity tests 15/15 with the validator
- [x] full regression (28 Oct, tags FINAL + FINAL2 on api-s69): 41 suites, 987 checks, 0 failed. 25 suites green on the first pass; 16 rerun as FINAL2 after fixes to the test harness only: suites with their own request code still sent `Bearer` (s63 s61 s59 s58 s45 s34 s33 s36 s30 s15 s16 s18 s20: proofs added, a proof per call before `-Parallel` blocks), the gate s56 needed the console's ten Arabic units, s41's separate short-clock Identity build was from 10 October (rebuilt by `s41\build-short.ps1`), and seven suites died with the console when the session ended. No product fault found. Finding under L3: Duende's own `/connect/userinfo` reads the token as Bearer only.
