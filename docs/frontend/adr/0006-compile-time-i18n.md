# ADR 0006: Compile-time translations with @angular/localize; language-neutral URLs; the BFF picks the build

- **Status:** Accepted — approved by the owner 2026-10-18 (D7). Where the language preference lives (D26) is deferred to phase 2 exit.
- **Source:** `docs/frontend/harmony-angular-architecture-v2.1.html`

## Context

Harmony ships Arabic and English. @angular/localize builds one bundle per locale; `ng serve` serves one locale at a time.

## Decision

Translate with `i18n`/`$localize` and stable custom ids. Build en and ar with per-locale configurations (`deployUrl` /en/ and /ar/, empty `baseHref`). App URLs are the same in both languages; the BFF serves the chosen locale's index.html. `i18nMissingTranslation: error` plus `tools/check-translations.mjs`, because an empty `<target>` silently ships English.

## Consequences

Switching language reloads the page (unsaved work is saved or discarded first). Two builds to test.

## Evidence

Spike: the ar build set `<html lang="ar" dir="rtl">`; ICU plural "3 حالات وصول"; deep link /p/GRD/arrivals identical in both languages; empty target → build passed with English; absent unit → build failed.

## How it is verified

F11, G10.
