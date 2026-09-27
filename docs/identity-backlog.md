# Identity Rebuild — Backlog

New solution: `D:\PMS System\Remy\Remy.slnx` (.NET 10, Xeleration.Core.BuildingBlocks).
Mode: tickets issued one at a time; each ticket is reviewed before the next.
Status legend: ⬜ todo · 🔵 in progress · ✅ done

## Epic 1 — Domain vocabulary (roles & permissions)
- 🔵 IDN-001 Role & Permission domain model (enums, invariants, behavior)
- ⬜ IDN-002 Permission catalogue + shipped role catalogue (seed data as code)
- ⬜ IDN-003 Catalogue integrity rules (SoD pairs, unheld permissions, startup checks design)

## Epic 2 — User accounts
- ⬜ IDN-010 UserAccount aggregate: two state axes, identity (Issuer, Subject), SecurityVersion
- ⬜ IDN-011 RoleAssignment child entity: per-property, time-boxed, revocable
- ⬜ IDN-012 Service principals & service credential (digest only)

## Epic 3 — Authorization
- ⬜ IDN-020 AuthorizationEvaluator + EffectiveAccess
- ⬜ IDN-021 Break-glass AccessGrant (≤12h, reason, no service principals)

## Epic 4 — Persistence foundation
- ⬜ IDN-030 Infrastructure project: DbContext, tenancy filter, audit, soft delete
- ⬜ IDN-031 EF configurations + initial migration (binary collation on Issuer/Subject)
- ⬜ IDN-032 Outbox + idempotency store
- ⬜ IDN-033 Security transaction + account lock (universal lock order)

## Epic 5 — Revocation spine
- ⬜ IDN-040 Revocation sequence + feed + reasons
- ⬜ IDN-041 Snapshots + gap tombstones
- ⬜ IDN-042 Feed API + shared-secret auth scheme

## Epic 6 — Application layer + API (core)
- ⬜ IDN-050 Handler project: pipeline (validation, logging, tenant guard)
- ⬜ IDN-051 Roles/Permissions features + controller
- ⬜ IDN-052 Users features + controller
- ⬜ IDN-053 Access features + controller
- ⬜ IDN-054 Startup checks (route/permission classification, catalogue integrity)

## Epic 7 — Sessions & tokens
- ⬜ IDN-060 OperatorSession aggregate + policy (30 min idle / 12 h absolute)
- ⬜ IDN-061 Signing keys + JWKS + discovery endpoints
- ⬜ IDN-062 Token issuance (claims model, RS256, exchange endpoint)
- ⬜ IDN-063 Federation (upstream OIDC schemes, browser challenge/complete, refresh/switch)
- ⬜ IDN-064 Sessions API + sweeper host

## Epic 8 — Step-up & maker-checker
- ⬜ IDN-070 StepUpAuthorization (bound, single-use) + PKCE + state protection
- ⬜ IDN-071 Maker-checker spine + execution host
- ⬜ IDN-072 Typed operations: bulk grant, link transfer

## Epic 9 — Federation identity model
- ⬜ IDN-080 FederatedAuthority + ExternalIdentityLink + link history
- ⬜ IDN-081 Authority bootstrap (config → persisted authorities)
- ⬜ IDN-082 Local staff enrolment (remy:local)
- ⬜ IDN-083 Tenant bootstrap (first administrator)

## Epic 10 — Directory provisioning (phased, last)
- ⬜ IDN-090 Provisioning connection + scope
- ⬜ IDN-091 Graph adapter + probe
- ⬜ IDN-092 Sync engine: lease/fencing, streams, runs, staging
- ⬜ IDN-093 Membership/eligibility + destructive guard
- ⬜ IDN-094 Merge approval + quarantine
- ⬜ IDN-095 Retention sweeps + telemetry

**Dropped from the old system (migration-era only):** cutover validation/backfill, read-cut readiness, dual-read resolution, migration review, duplicate repair, legacy `UserStatus` projection.
