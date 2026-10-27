# ADR 0010: One intent key per user action; retries only inside the 10-minute receipt window

- **Status:** Accepted on evidence — 2026-10-18
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

The framework's receipt gate runs a write once per `Idempotency-Key` (tenant + user scope), replays the answer for 10 minutes, answers 409 000.06 while in flight and 422 000.07 for a reused key with a different body.

## Decision

Every write button creates one key per press and reuses it for that intent's retries. GET: at most 2 automatic retries. Keyed writes: retry with the same key for network errors, 503 and 409 000.06, inside 10 minutes only. Unkeyed writes and payments are never retried automatically.

## Consequences

After 10 minutes a retry is a new intent and must re-read state first. Payments need an outcome query (D8).

## Evidence

Harmony.Core/Middlewares/Receipts/ReceiptGate.cs, ReceiptSettings.cs.

## How it is verified

G17: 0 duplicate commands under fault injection.
