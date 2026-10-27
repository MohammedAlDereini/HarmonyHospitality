# Harmony frontend ADRs

Decisions behind `docs/frontend/harmony-angular-architecture-v2.1.html`. One decision per file.

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-angular-22-zoneless.md) | Angular 22.2.x, zoneless, standalone, OnPush; TypeScript 6.0.x; Node 24 | Accepted on evidence — 2026-10-18 |
| [0002](0002-workspace-and-folder-libraries.md) | One Angular CLI workspace at Frontend/web; libraries are folders with an index.ts; no Nx | Accepted — approved by the owner 2026-10-18 (D16) |
| [0003](0003-two-applications-two-hosts.md) | Two applications on two hostnames with two BFF clients | Accepted — approved by the owner 2026-10-18 (D17) |
| [0004](0004-boundary-checker.md) | Library boundaries and cycles enforced by our own checker | Accepted — approved by the owner 2026-10-18 (D24) |
| [0005](0005-server-state-and-stores.md) | Server state with Angular resources and plain signal stores; no store library | Accepted on evidence — 2026-10-18 |
| [0006](0006-compile-time-i18n.md) | Compile-time translations with @angular/localize; language-neutral URLs; the BFF picks the build | Accepted — approved by the owner 2026-10-18 (D7). Where the language preference lives (D26) is deferred to phase 2 exit. |
| [0007](0007-nonce-csp-trusted-types.md) | Per-request nonce CSP with Trusted Types; no <base> element | Accepted — approved by the owner 2026-10-18 (D14) |
| [0008](0008-cdk-aria-design-system.md) | Design system on Angular CDK and Angular Aria behind ui wrappers; no Material | Accepted on evidence — 2026-10-18 |
| [0009](0009-tokens-and-fonts.md) | Design tokens from home(8); the prototype's system font stacks; no font files shipped | Accepted — approved by the owner 2026-10-18 (D18, D19) |
| [0010](0010-intent-keys-and-retries.md) | One intent key per user action; retries only inside the 10-minute receipt window | Accepted on evidence — 2026-10-18 |
| [0011](0011-property-in-url.md) | Property in the URL; property data keyed and cleared explicitly | Accepted on evidence — 2026-10-18 |
| [0012](0012-first-party-telemetry.md) | First-party telemetry through the BFF; no browser OpenTelemetry SDK yet | Proposed — waits for D9 (by phase 2 exit) |
| [0013](0013-supply-chain-policy.md) | Supply-chain policy | Accepted — approved by the owner 2026-10-18 (D23) |
| [0014](0014-testing-and-gates.md) | Testing pyramid and release gates G1–G20 on GitHub Actions | Proposed — the gates are approved with the architecture; the CI platform waits for D22 (before the first Qa release) |
| [0015](0015-no-ssr-sw-mfe.md) | No SSR, no service worker, no micro-frontends for the staff and console apps | Accepted on evidence — 2026-10-18 |
