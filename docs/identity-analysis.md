# Identity Service — Analysis of the Old System

Source: `D:\source\repos\Remy\services\Identity\` (~282 files, 6 projects).
Layering: `Domain → Handler → Api`, `Infrastructure → Domain`, `Api` references both. `Contracts` holds wire types only.

## Functional modules

| # | Module | What it does | Keep? |
|---|--------|-------------|-------|
| M1 | **Permission & role catalogue** | Code-defined catalogue (~750 permission codes across all services, ~32 shipped roles). Roles carry a `PrivilegeLevel` (Standard/Supervisor/Manager/Administrator); Manager+ ⇒ MFA required. Seeding materialises the catalogue into a tenant. | ✅ Core |
| M2 | **User accounts & role assignments** | `UserAccount` holds no credential — identity is `(Issuer, Subject)` from an external OIDC provider. Two independent state axes: `DirectoryLifecycleState` (Active/Suspended/Terminated) × `AdministrativeAccessState` (Enabled/Disabled/Locked). Role assignments are per-property and time-boxed. `SecurityVersion` bumps on every authority change — the anchor of revocation. | ✅ Core |
| M3 | **Authorization evaluation** | "May subject do X at property P" — combines role permissions + elevations, returns `EffectiveAccess` with an `AccessSource` per allow. | ✅ Core |
| M4 | **Break-glass elevation** | Time-boxed grant of one permission at one property, max 12h, mandatory reason. Exists so no standing super-admin ships. | ✅ |
| M5 | **Operator sessions** | Server-owned sign-in record keyed on the upstream authentication instant. Idle timeout 30 min, absolute ceiling 12h. One upstream sign-in = one session regardless of tokens minted. | ✅ Core |
| M6 | **Token issuance & federation** | Federates authentication to Entra/Google/OIDC; mints own RS256 token with tenant/property/role/permission claims. Bearer exchange, browser OIDC + handshake cookie, refresh/switch, machine credential. | ✅ Core |
| M7 | **Step-up authorization** | Fresh, bound, single-use second authorization per destructive operation (bound to operation + target + payload digest). PKCE round trip, AES-GCM protected state. | ✅ |
| M8 | **Provisioning connections & scope** | One non-retired Graph connection per tenant; lifecycle Draft→…→Retired; explicit group allow-list. | ✅ Later |
| M9 | **Directory sync engine** | Joiner-mover-leaver: fenced lease, delta-cursor streams, staged run items, dispositions, destructive-narrowing guard. Biggest subsystem (~12k lines of domain). | ✅ Later |
| M10 | **Identity links & federated authorities** | `ExternalIdentityLink (TenantId, AuthorityId, ProviderSubject) → account` is what sign-in resolves against. `FederatedAuthority` is the durable half of federation. | ✅ |
| M11 | **Merge approval & quarantine** | Human review when sync would merge identities; approval needs a fresh provider read + fencing check. | ✅ Later |
| M12 | **Maker-checker** | Generic two-person approval spine. No generic create endpoint — typed operations only (bulk grant, reactivation, quarantine release, link transfer). Background host executes approved children. | ✅ |
| M13 | **Revocation** | Per-tenant monotonic sequence (explicit row lock) → feed → snapshots → gap tombstones. Other services poll it or subscribe via broker relay. Machine-only, shared-secret auth. | ✅ Core |
| M14 | **Security transactions** | All security mutations in a retried transaction with a universal lock order: per-account app lock → tenant revocation-sequence row. Fresh DbContext per retry attempt. | ✅ Core |
| M15 | **Local staff enrolment** | `remy:local` issuer for staff with no corporate directory. Invitation → redeem → account. PII held only as keyed digest. | ✅ |
| M16 | **Tenant bootstrap** | The one route that acts in a tenant the caller's token doesn't name: first administrator of a new tenant. | ✅ |
| M17 | **Security audit** | Append-only audit event store, closed event-type vocabulary, enforced by EF interceptor. | ✅ Core |
| M18 | **Retention** | Hourly sweeps: staged payloads, terminal step-ups, revocation feed prefix, idempotency claims. | ✅ |
| M19 | **Development bootstrap** | Dev-only catalogue seeding + `POST /Tokens/development`. | Optional |
| M20 | **Cutover / migration** | Cutover validation, link backfill, legacy-status migration, migration review queue, duplicate repair, read-cut ledger, dual-read resolution. ~6,500 lines prod + ~5,000 test. | ❌ **Drop — greenfield doesn't migrate** |

> **Harmony decisions, 27 Sep 2026 (they override the table above):**
> - **M4 no longer means "no super-admin".** Every installation ships one standing `SUPERADMIN` role in the platform tenant, with full access to everything across all tenants. It is used for demos and to manage system configuration for other tenants. Break-glass stays for everyone else.
> - **M2/M15 local sign-in uses a password.** It is held by the local authority, never on `UserAccount`. Microsoft and Google come later as more authorities.

## Core domain model (short form)

- **UserAccount**: `Issuer`, `Subject`, profile fields, `DirectoryState` × `AdministrativeState` (two axes, never write each other), `IsMfaEnrolled`, `IsServicePrincipal`, `ServiceCredentialSecretHash`, `SecurityVersion`. Unique on `(TenantId, Issuer, Subject)` under **binary collation**. Owns `RoleAssignment[]` (`RoleCode`, `PropertyId`, valid-from/until, revocation fields).
- **Role**: `Code`, `NameEn/Ar`, `PrivilegeLevel`, `IsSystemRole`, `PermissionCodes[]`. `IsPrivileged` = Manager+ ⇒ MFA.
- **Permission**: `Code`, `DescriptionEn/Ar`, `Category`, `IsPropertyScoped`.
- **AccessGrant** (break-glass): user + permission + property + reason + grantor, ≤12h, soft revoke.
- **OperatorSession**: keyed (user, upstream auth instant); Live/Idle/Elapsed/Ended; expiry computed at point of use.
- **FederatedAuthority** → **ExternalIdentityLink** (+ period history) → account.
- **ProvisioningConnection** → approved groups → stream states (cursors) → **DirectorySyncRun** → run items → membership edges.
- **MakerCheckerRequest** (Pending→Executed/Rejected/Expired/Superseded) 1:1 **MakerCheckerExecution** with per-target children.
- **StepUpAuthorization**: Pending→Completed→Consumed; single use; bound to payload digest.
- **AuthorizationRevocationSequence** (per-tenant, monotonic) → **RevocationFeed** rows → snapshots → gap tombstones.

## Key invariants worth carrying over

1. Directory establishes **identity, never authority** — no role/permission crosses the directory seam.
2. Two account axes never write each other; the old single `Status` was only a projection (dropped in the rebuild).
3. Every disable/terminate/role-revoke writes the revocation feed **inside** the same security transaction.
4. Lock order is universal and coded: account app-lock first, then tenant revocation-sequence row.
5. `MapInboundClaims = false` on every JWT scheme; RS256 only, HMAC refused at startup.
6. Shipped roles' permission codes must exist in the catalogue; segregation-of-duties pairs never combined; every catalogued permission is held by a role or explicitly declared unheld — all checked at startup.
7. Approval = execution (no observable "Approved" state in maker-checker); maker ≠ checker; rows fenced on captured rowversion.
8. Token `sub` is REMY's own account id, never the provider's.
9. No credential is ever stored — service secrets and invitation handles as digests only.

## Background hosts

| Host | Cadence | Purpose |
|------|---------|---------|
| SessionSweeper | 5 min | Record elapsed/idle sessions ended (+ revocation) |
| DirectorySyncHost | 5 min | One sync command per active connection |
| MakerCheckerExecutionHost | 30 s | Apply approved execution children |
| ProvisioningRetentionHost | 1 h | Retention sweeps |
| RevocationRelayHost | 5 s poll | Push feed to broker (only if configured) |
| AuthorityBootstrap | at boot | Reconcile configured providers vs. persisted authorities |

## API surface (old): controller inventory

Roles, Users (+UserProvisioning: bulk-grants, directory-reactivate, emergency-revoke), Access (check/effective/me/elevations/posture), UserAccounts/{id}/state, Sessions, Tokens (exchange/service/step-up/challenge/complete/refresh/switch/signout/providers), StepUp, Discovery (.well-known), Revocations (feed/snapshot), ProvisioningConnections, ProvisioningRuns (+merges +quarantine), FederatedAuthorities (+repoint), IdentityLinks, MakerChecker (no create route), LocalStaff, PermissionNames, TenantBootstrap, SecurityOperations/{id}/status. Dropped: Cutover, CutoverReadiness, MigrationReview, DuplicateRepair.
