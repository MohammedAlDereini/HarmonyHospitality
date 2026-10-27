# ADR 0015: No SSR, no service worker, no micro-frontends for the staff and console apps

- **Status:** Accepted on evidence — 2026-10-18
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

Both apps sit behind sign-in with personal data; money and room operations must not be queued offline; one team ships one release train.

## Decision

Client-rendered SPAs served by the BFF. A future guest booking engine (SSR) or POS terminal app (offline) would be separate applications with their own ADRs.

## Consequences

No Node server; no cache of personal data in the browser.

## Evidence

Architecture v2.1 §19.

## How it is verified

Review.
