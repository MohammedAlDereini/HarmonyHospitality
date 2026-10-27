# ADR 0007: Per-request nonce CSP with Trusted Types; no <base> element

- **Status:** Accepted — approved by the owner 2026-10-18 (D14)
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

Angular inserts `<style>` elements at runtime; the BFF page policy `style-src 'self'` blocks them. The BFF policy also has `base-uri 'none'`, which blocks the CLI's `<base href>`.

## Decision

BFF change B1: render index.html per request, replacing `__CSP_NONCE__` with a fresh 128-bit nonce; page CSP `default-src 'self'; script-src 'self' 'nonce-…'; style-src 'self' 'nonce-…'; img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'; require-trusted-types-for 'script'; trusted-types angular angular#bundler`; index.html `Cache-Control: no-store`. The app has no `<base>`; `APP_BASE_HREF` is `/`; assets use per-locale `deployUrl`. Report-only first, enforce after a clean week in Qa.

## Consequences

index.html is never cached. No `bypassSecurityTrust*`, no HTML sinks (F8).

## Evidence

Spike: today's policy left the app unstyled (1 inline script + 4 inline styles blocked); the B1 policy gave 0 violations with CDK overlay, Angular Aria and lazy chunks; Trusted Types enforced (a string to innerHTML threw); deep links failed with `<base>` and worked without it.

## How it is verified

G12 (0 CSP/TT violations for 7 Qa days), G19 (nonce differs per request).
