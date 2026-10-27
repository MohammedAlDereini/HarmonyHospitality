# ADR 0013: Supply-chain policy

- **Status:** Accepted — approved by the owner 2026-10-18 (D23)
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

Every third-party package needs the owner's yes (rule of 2026-10-07). ASVS 15.1.1 and 15.1.2 ask for remediation time frames and an SBOM.

## Decision

Approved register; licence allowlist (MIT, Apache-2.0, ISC, BSD-2/3, 0BSD, Unlicense; CC-BY-4.0 for data; MPL-2.0 for unmodified test tools); frozen lockfile; `minimumReleaseAge: 4320` with exact-version fast lane; `trustPolicy: no-downgrade`; `strictPeerDependencies`; `blockExoticSubdeps`; `allowBuilds: { esbuild: true }` only; pnpm audit + OSV in CI; CycloneDX SBOM per release via `pnpm sbom`; remediation targets proposed in D28.

## Consequences

Fresh releases wait 3 days unless fast-laned by a reviewed PR. Every package sits behind a wrapper so it can be replaced.

## Evidence

Spike: release-age gate refused 6 packages younger than 72 h; exact exclusions worked; build and tests passed with only esbuild's install script; `pnpm sbom` produced CycloneDX 1.7 with 94 components.

## How it is verified

F13, G13.
