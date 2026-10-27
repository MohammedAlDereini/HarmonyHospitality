# ADR 0011: Property in the URL; property data keyed and cleared explicitly

- **Status:** Accepted on evidence — 2026-10-18
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

Property codes are unique per tenant (UX_Properties_TenantId_Code). Route injectors are not destroyed on navigation unless the opt-in `withAutoCleanupInjectors` (22.2) is configured.

## Decision

Routes use `/p/{propertyCode}/…`. Property-scoped caches are keyed by tenant + property and cleared by `propertyScope()` on every switch and sign-out; no correctness depends on an injector dying.

## Consequences

Deep links work across tabs and languages. withAutoCleanupInjectors only after the soak test proves it.

## Evidence

router.d.ts 22.2.3 (`withAutoCleanupInjectors` @publicApi 22.2); PropertyConfiguration.

## How it is verified

P10 switch test; G12 URL tampering test.
