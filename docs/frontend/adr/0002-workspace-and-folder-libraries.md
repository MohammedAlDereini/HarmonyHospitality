# ADR 0002: One Angular CLI workspace at Frontend/web; libraries are folders with an index.ts; no Nx

- **Status:** Accepted — approved by the owner 2026-10-18 (D16)
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

Harmony ships one product from one repository. Built libraries (ng-packagr) add a build, version and publish step per library. Nx's Angular plugin peers @angular/build <23.

## Decision

Workspace `Frontend/web` with applications `harmony-web` and `harmony-console`, selector prefix `hr`. Libraries are folders under `libs/` reached only through `@harmony/<group>/<lib>` path aliases mapped to their `index.ts`. A library is created when its first file is needed. The React app in `Frontend/harmony-web` stays until parity, then is deleted in one commit.

## Consequences

No per-library builds or versions. Boundaries must be enforced by a check (ADR 0004). A library that must be shared outside the workspace graduates to a built package by a new ADR.

## Evidence

Architecture v2.1 §4; registry: @nx/angular 23.3.0 peers @angular/build >=20 <23.

## How it is verified

G1, G3.
