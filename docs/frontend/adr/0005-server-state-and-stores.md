# ADR 0005: Server state with Angular resources and plain signal stores; no store library

- **Status:** Accepted on evidence — 2026-10-18
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

`resource`, `rxResource` and `httpResource` are @publicApi 22.0. Global state is limited to session, access, property context and preferences.

## Decision

Domain data-access libraries own server state as signal-based store classes over resources, keyed by tenant + property + filters, LRU-bounded, invalidated by own writes, hub events, sequence gaps and property switch. @ngrx/signals only by a later ADR.

## Consequences

No third-party store. Store classes follow one pattern (§7.8).

## Evidence

Type definitions of 22.2.3; architecture v2.1 §9.

## How it is verified

G4–G6; P2 network assertions (requests cancelled when leaving a page).
