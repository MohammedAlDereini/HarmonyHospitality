# ADR 0012: First-party telemetry through the BFF; no browser OpenTelemetry SDK yet

- **Status:** Proposed — waits for D9 (by phase 2 exit)
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

OpenTelemetry states that browser client instrumentation is experimental and mostly unspecified. Sentry's Angular SDK peers @angular/core <=22.x and is a SaaS.

## Decision

Errors and Web Vitals go to `POST /bff/telemetry` (B5: session required, a minimal rate-limited anonymous beacon for signed-out pages, 8 KB cap, scrubbed) and on to the log pipeline. Our interceptor writes W3C `traceparent`. `web-vitals` (approved) is added with B5.

## Consequences

No third-party script in the browser. Revisit OpenTelemetry when its browser support is stable.

## Evidence

opentelemetry.io/docs/languages/js; registry metadata.

## How it is verified

Payload inspection test against personal-data fixtures.
