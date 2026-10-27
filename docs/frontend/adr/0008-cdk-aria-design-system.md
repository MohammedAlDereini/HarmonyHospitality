# ADR 0008: Design system on Angular CDK and Angular Aria behind ui wrappers; no Material

- **Status:** Accepted on evidence — 2026-10-18
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

Harmony keeps its own appearance. CDK provides overlay, dialog, focus trap, live announcer, bidi, virtual scroll and table. Angular Aria provides headless WAI-ARIA patterns but carries no stability tag yet.

## Decision

Behaviour from CDK and Aria; appearance only from `libs/ui/tokens`. Feature code imports our `ui/*` components, never CDK or Aria directly.

## Consequences

A change in Aria's young API touches one library.

## Evidence

Spike: Aria listbox rendered role=listbox with 3 options; CDK connected overlay opened under Trusted Types with no violations.

## How it is verified

G5 harness tests, G9 axe and keyboard suites, G11 visual baselines.
