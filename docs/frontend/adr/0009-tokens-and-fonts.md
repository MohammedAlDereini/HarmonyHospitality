# ADR 0009: Design tokens from home(8); the prototype's system font stacks; no font files shipped

- **Status:** Accepted — approved by the owner 2026-10-18 (D18, D19)
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

home(8).html is the most recent prototype and the one prepared for extraction. Its fonts are local system fonts. Segoe UI Variable ships only with Windows 11. Dubai is not in the Windows 11 font lists; the copy on the development PC is a "Microsoft supplied font" whose embedded licence prohibits any use beyond the Microsoft product's terms.

## Decision

Copy home(8)'s `:root`, dark and density tokens verbatim into `libs/ui/tokens`. Keep the stacks: Latin "Segoe UI Variable Text","Segoe UI",system-ui,-apple-system,sans-serif; Arabic "Dubai","Segoe UI",Tahoma,system-ui,sans-serif. Ship no font files.

## Consequences

Arabic renders in Segoe UI or Tahoma where Dubai is absent. Visual baselines are taken on the reference hotel PC and on a PC without Dubai. Revisit only with the publisher's written licence or a re-approved open Arabic face.

## Evidence

home(8).html `:root`; learn.microsoft.com Windows 11 font list; DUBAI-REGULAR.TTF name table and OS/2 fsType.

## How it is verified

F16 (colours only in tokens), G11.
