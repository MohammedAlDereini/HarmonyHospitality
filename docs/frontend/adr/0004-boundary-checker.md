# ADR 0004: Library boundaries and cycles enforced by our own checker

- **Status:** Accepted — approved by the owner 2026-10-18 (D24)
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

eslint-plugin-boundaries 7.2.0 pins handlebars 4.7.9 (two critical advisories, fixed upstream in 4.7.10) and micromatch 4.0.8 → braces 3.0.3 (one high advisory, no fix); pnpm audit reported 1 moderate, 1 high, 2 critical.

## Decision

`tools/check-architecture.mjs` (about 120 lines, Node + the TypeScript compiler only) enforces: entry-only imports, layer rules, cross-domain only from a feature into another domain's data-access, framework-free shared and domain layers, app isolation, no cycles (Tarjan). ESLint core `no-restricted-imports` gives editor feedback. Sheriff 0.20 is the fallback.

## Consequences

We maintain a small script. No third-party plugin with transitive risk.

## Evidence

Spike: on a 10-library fixture the checker reported all 6 planted defects and exited 0 on the cleaned tree.

## How it is verified

F1–F5 in G3 on every PR.
