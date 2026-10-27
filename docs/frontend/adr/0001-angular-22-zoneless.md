# ADR 0001: Angular 22.2.x, zoneless, standalone, OnPush; TypeScript 6.0.x; Node 24

- **Status:** Accepted on evidence — 2026-10-18
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

Harmony needs one frontend platform for 10+ years. Angular 22 is the active major (active to 2027-06, LTS to 2028-06; from v22 one major per year). Angular 22.2.3 accepts only TypeScript >=6.0 <6.1 and Node ^22.22.3 || ^24.15.0 || ^26.0.0.

## Decision

Use Angular 22.2.x pinned to one exact version, zoneless change detection (the default), standalone components only, OnPush everywhere, signals for state. TypeScript 6.0.3, Node 24.21.0 (.nvmrc and CI image). Adopt each Angular major within 3 months of release; never run past LTS.

## Consequences

No zone.js. Developer-preview and experimental APIs stay out of foundation code. TypeScript 7 waits until Angular's peers allow it.

## Evidence

angular.dev/reference/releases and /reference/versions; spike: `ng new` 22.2.3 generated no zone.js, Vitest + jsdom, TypeScript ~6.0.2; a zoneless test asserted no `Zone` global.

## How it is verified

F7 (no zone.js), F6 (OnPush lint), G1/G2.
