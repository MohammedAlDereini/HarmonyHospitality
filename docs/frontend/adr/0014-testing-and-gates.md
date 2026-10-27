# ADR 0014: Testing pyramid and release gates G1–G20 on GitHub Actions

- **Status:** Proposed — the gates are approved with the architecture; the CI platform waits for D22 (before the first Qa release)
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

The Harmony repository has no pipeline; the repository and the Harmony package feed live on GitHub.

## Decision

Gates G1–G20 (architecture v2.1 §16) run in GitHub Actions with actions pinned by commit hash and no secrets in the frontend job.

## Consequences

Without CI no gate can block a release.

## Evidence

Repository listing; nuget.config.

## How it is verified

The gates themselves.
