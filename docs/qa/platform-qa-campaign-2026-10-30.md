# Harmony platform — production QA campaign (30 Oct 2026)

Applying the *Universal Production QA Playbook* v2.0 (30 Oct 2026) to the Harmony platform as it stands on this
machine. One run, one day, one person in every role (see §4): this is a qualification pass on the exact candidate
below, not a release certificate. Every number carries its denominator; what was not run says so.

**Candidate.** Repository `D:\PMS System\Harmony`, branch `duende-identity-server`, commit `5878ecb` (28 Oct) plus
**171 uncommitted paths** (the console redesign batches 1–3, the Identity doors they needed, the CSV fix of §11 and
this campaign's documents). Identity published to `D:\PMS System\Harmony.TestEnv\api-s77` (30 Oct 11:33) from that
tree with the framework packages in `packages-local`; frontends built from the same tree (console 479.7 kB, hotel app
430.9 kB initial). Environment `Development` settings: database `HarmonyIdentity_HANDS` on the local SQL Server 2025
Developer (17.0.4040), Redis 7 and smtp4dev in Docker, RabbitMQ up but **messaging off** in this candidate's settings
(no EventBus section), Aspire dashboard off. Hosts: Identity `https://localhost:5000`, hotel app BFF `:5210`,
console BFF `:5220`. Evidence: `D:\PMS System\Harmony.TestEnv\s72\evidence\*` (JSON and text), the suites' own logs
under `s35\regression-PB1`, and this document.

## 1. Application profile (playbook §2)

| Area | Harmony |
| --- | --- |
| Product and users | A hospitality platform. Service 1, **Identity**, is built: tenants (hotel companies), their hotels, subscriptions (module lines), reference data, people, roles and permissions, sign-in, devices, approvals, directory sync. Two web apps: the **hotel app** for a customer's staff and the **platform console** for Harmony's own people. Consequence of failure: a hotel cannot sign in or administer its people; the platform cannot onboard or bill customers; a cross-tenant leak exposes another company's data. |
| Platform | ASP.NET Core 10 Identity service (Duende IdentityServer, EF Core 10, SQL Server), two YARP BFFs (cookie sessions, DPoP-bound tokens, CSP), two Angular 22 single-page apps (en + ar builds), Redis (locks, rate limits), RabbitMQ (outbox, off here), SMTP relay. |
| Scope of this campaign | Identity API, both BFFs, both apps, the local database, the local containers. Excluded: the 13 other services (not built), the Azure platform (proposal only), real mail, real devices. |
| Requirements | `docs/identity-aggregates.html`, `docs/identity-erd.html`, `docs/frontend/*` (architecture v2.2), the Figma console (RMIE3aJj2QXdLwSlHg9xu9) and the hotel prototype, the QA records in `docs/qa`, the security audit in `docs/security`. Open questions are the owner decisions listed in those records (console redesign §5 and §6.4). |
| Roles and boundaries | Anonymous; hotel staff (per-tenant permissions, per-hotel access); tenant administrator (ADMINISTRATOR system role, born with the tenant); platform people (platform tenant HARMONY: SUPERADMIN system role, custom roles); service accounts (the two BFF clients with key-based client authentication); the SCIM directory connection. Rule: platform people never enter a hotel; a hotel account is refused by the console. |
| Architecture | Clients → BFF (cookie, CSRF header, token exchange, DPoP) → Identity (JWT + DPoP, permissions, tenant context) → SQL Server. Redis for distributed locks and rate limits; RabbitMQ outbox for integration events (off in this candidate); SMTP for invitations and two-step mails. |
| Data | Personal data: names, e-mails, phone, device fingerprints, sign-in history (restricted: password hashes, two-step secrets, passkeys). Retention rules: not defined yet (security trail, sessions). Counts here: 10 tenants, 3 hotels, 10 accounts after the fixtures. |
| Database observation | `harmony_qa_observer`, a SQL login mapped into `HarmonyIdentity_HANDS` with `db_datareader` only; INSERT/UPDATE/DELETE/ALTER/EXECUTE/CREATE denied and **proven refused** (errors 229 and 262) before any observation (`s72\observer-setup.sql`, `observer-fix.sql`). Primary database, no replica, no masking, no row-level security. Queries are versioned in `s72\tests\e2e\playbook-e2e.ps1` and kept with every result in the evidence JSON. |
| Expected results | Independent of the implementation: the submitted request bodies (lossless storage), arithmetic over the observer's rows (summaries), the playbook's own rules (refusals, idempotency), and the framework's published error-code catalogue. No business owner reviewed a reference dataset in this campaign (see §4). |
| Transactions | Tenant birth (tenant + administrator + invitation mail), subscriptions (module lines with rooms), suspend/reinstate, role and permission changes, password set from a mailed link, sign-in and sign-out. No money moves in Identity. |
| Support matrix | Promised: Chrome/Edge/Firefox/Safari current, 1366×768 at 125 % floor, en and ar, light and dark. Available here: Chrome 156 (browser pane and headless). |
| Usage model | Not agreed (no production yet). The load run below uses an assumption: 10 concurrent platform people for 60 s, 60 % summary reads, 20 % customer lists, 20 % trail pages. |
| Service objectives | Not agreed. The Azure proposal names 99.9 % as an honest target; nothing here measures availability. |
| Recovery | RTO/RPO not agreed. Measured: a copy-only backup + restore + Identity start = **15.8 s** for a 1.1 MB database on this laptop (§10). |
| Obligations | ASVS 5.0 level (D20) and WCAG 2.2 AA (D29) are pending owner decisions; no payment, health or public-sector obligations apply to Identity. |
| Release | Artifact: the publish folder `api-s77` and the two `dist` builds; deployment method and rollback: the Azure proposal (not executed). Release authority: the owner. |

## 2. Standards applicability and evidence register (playbook §3)

| Reference | Applicability | Implementing control | Evidence here | Status |
| --- | --- | --- | --- | --- |
| ISO/IEC/IEEE 29119 (concepts, processes) | Applicable as vocabulary and structure | This document's plan → design → execution → evidence → decision | Sections 5–12 | Used as structure; no conformity claim |
| OWASP ASVS 5.0 | Level unresolved (D20); L2 requested in the frontend rules | Session, authentication, access control, input handling controls in Identity and the BFFs | `docs/security/owasp-top10-2025-audit-2026-10-22.md`, §10 below | Partially verified; no requirement-ID register yet — **gate 15 open** |
| OWASP API Security Top 10:2023 | Applicable | Object/function authorization, resource limits | PB-10..PB-14, PB-23 (page size cap 100) | Verified for the doors exercised |
| WCAG 2.2 AA | Target proposed (D29) | Skip link, focus order, Escape per layer, visible focus, reflow | §7 M12 | Human checks on a few flows; no automated scan (tool not installed) — **open** |
| NIST SSDF | Applicable to the delivery process | PR checks (`run check`), dependency audits | §8, §10 | Audits clean; CI not running (draft workflow) |

## 3. People and independence (playbook §4)

One person (the assistant, on the owner's instruction) performed QA lead, manual tester, automation engineer,
security and performance specialist, DBA for the observer, and operator for the faults and restore. The developer
of the console redesign is the same person. **No independent business acceptance happened**: the owner reviews the
working tree and this record; gate 9 (business acceptance) stays open until they do.

## 4. Inventory and traceability (playbook §5)

Dispositions: Covered (a suite or spec exercises it on this candidate), Gap (owner), Not applicable (reason).

### 4.1 Screens

| App | Place | Routes | Covered by | Disposition |
| --- | --- | --- | --- | --- |
| Console | Home | `/` | s71 (home, dark, ar, phone, tablet), hands-on §6.3 of the redesign record | Covered |
| Console | Tenants, tenant page, Add module, New tenant wizard | `/tenants`, `/tenants/:id`, `/tenants/new`, `/onboarding` | s56 gate (wizard, suspend/reinstate, modules), s71, PB-01..25 (doors), UI-01..08 | Covered |
| Console | Properties, property page | `/properties`, `/properties/:id` | s56 gate (new property), s71, PB-06/08/20 | Covered |
| Console | Groups, group page | `/groups`, `/groups/:id` | s56 gate (new group, join), s71 | Covered |
| Console | Reference index and 13 tables | `/reference`, `/reference/:table` | s56 gate (currencies, settings registry), s71, s23 | Covered |
| Console | Audit log, Sign-ins, Health | `/trail`, `/sign-ins`, `/health` | s71, s70, s62, s34 | Covered (decisions 3–5, 9–12 of the redesign record stand) |
| Console | People, person, invitations, approvals and rules, company login, directory | `/people`, `/people/account/:id`, `/people/invitations`, `/people/security-rules`, `/people/company-login`, `/people/directory` | s71, s64, s61, s65, s69, PB-11/22 | Covered |
| Console | Roles matrix, Settings | `/roles`, `/settings` | s71, s12, s59 | Covered (Settings shows its empty state: no registered key in this seed) |
| Console | Sign-in and refusal pages (Identity) | `/account/login`, refusal | s30, s68, headless capture, UI-09 | Covered |
| Console | Signed-out, not found | `/signed-out`, `**` | UI-06, UI-07 | Covered |
| Hotel app | Chooser, deep link, admin users/roles/settings, hello | `/`, `/p/:property/admin/*`, `/hello` | s56 gate (44 checks), s54, s58 | Covered |
| Both | **Forgot password page** | `/forgot-password` (linked from the sign-in page) | UI-07 | **Gap — FND-03** |

### 4.2 API doors (Identity, 27 controllers)

| Controller(s) | Suites on this candidate (regression run PB1) | Added by this campaign |
| --- | --- | --- |
| Tenant (Create, InviteAdministrator, Update, Suspend, Reinstate, Subscribe, Unsubscribe, Summary, GetAllPaged, GetById, GetMine) | s46, s48, s52, s70, s56 | PB-01..05, 07, 10, 12, 15..19, 23 |
| Property (Create, Summary, GetAllPaged, GetById, GetMine, GetMineById, Update, UpdateContacts) | s51, s53, s70 | PB-06, 08, 10, 20 |
| PropertyGroup, PropertyGroupType | s57 | — |
| UserAccount (create, state, roles, password, two-step, devices, sessions, events, get-all, forgot/reset) | s3a, s15, s16, s17, s18, s31, s33, s37, s43, s45, s69 | PB-02, 04b, 11, 22 |
| Role (+ permissions) | s5, s10, s12, s13 | — |
| SecurityAudit (get-all, counts) | s34, s62, s70 | PB-03, 21, 24, 25 |
| Approval, Directory/Scim, CompanyLogin, Revocation | s64, s65, s61, s63 | — |
| ConfigSetting, ConfigValue | s59 | — |
| City, Country, CountrySubdivision, Currency, Language, LookupCategory, LookupValue, Module, PropertyType, RegulatoryEnvironment, TimeZone | s22, s23, s50 | — |
| Invitation (mine, accept, decline) | s48 | — |
| BFFs (`/bff/login`, `/bff/logout`, `/bff/user`, `/bff/flags`, `/healthz`, `/.well-known/*`, proxy) | s27, s40, s41, s54, s55, s56 | SEC-01..04, UI-08 |

### 4.3 Data, jobs and infrastructure

| Item | Covered by | Note |
| --- | --- | --- |
| 73 tables of `HarmonyIdentity_HANDS` (24 migrations applied) | Observer queries PB-01..23; restore reconciliation RS-03 (20 business tables key by key) | Volatile tables (sessions, grants, trail, devices, keys) compared by count only |
| Outbox / inbox (RabbitMQ) | s60 (own settings) | **Off in this candidate** — no fault test possible here |
| Receipts (idempotency store) | PB-15, s36 | Redis-backed; see FT-01 |
| Revocation feed, rate limits, two-step mails | s63, s35, s20 | — |
| Backup / restore | RS-01..06 | Local SQL Server only |
| Deployment, migration ordering, certificates | **Not run** | No deployment target exists yet (Azure proposal) |

## 5. Risk register (playbook §6; impact × likelihood, 1–5 each; critical override for isolation and disclosure)

| Risk | Score / tier | Evidence on this candidate | Residual |
| --- | --- | --- | --- |
| R-01 Cross-tenant read or write through any door | 5×2 → **critical override** | PB-10, PB-11, PB-12, s53, s58, s70: every attempted cross-tenant read refused (403 000.4, 202 00092/00036), lists hold only the caller's tenant | Tested doors only; not every door of every controller |
| R-02 Token or proof misuse (replay, tampering, scheme downgrade) | 5×2 → critical | PB-13 (Bearer, no proof, tampered, none → 401 except **FND-01**), PB-14 (replayed proof refused), s68 | FND-01 is a 500 instead of a 401 in Development only |
| R-03 Lost or duplicated business effect on retry or race | 4×3 = 12 high | PB-15 (receipt replay, mismatch 000.07), PB-16 (same code at the same moment → one row), PB-17 (lifecycle race → consistent) | Only Tenant/Create and Suspend/Reinstate raced |
| R-04 Wrong numbers on the console (summaries vs truth) | 4×3 = 12 high | PB-19..22 reconcile Tenant/summary, Property/summary, trail counts, people census against the observer exactly | Populations defined in §9 |
| R-05 Password recovery unusable | 4×3 = 12 high | UI-07: the link lands on a 404 in both apps — **FND-03** | Open |
| R-06 Data loss / no restore path | 5×2 = 10 medium (override: essential service) | RS-01..06: backup, restore, key-for-key reconciliation, sign-in on the restored database | Local only; no off-site copy, no RPO/RTO agreed |
| R-07 Accessibility blocks an essential task | 3×3 = 9 medium | UI-01..05: skip link, menu and drawer focus, Escape, reflow at 200 % | No screen-reader pass, no automated scan |
| R-08 Dependency with a known vulnerability | 4×2 = 8 medium | NuGet: Identity and BFF none (transitive included); npm production none; **6 dev-only advisories in vite 8** (FND-05) | Dev tooling only |
| R-09 Cache or relay outage takes the service down | 4×2 = 8 medium | FT-01 (Redis gone → slow 401s, **FND-07**), FT-02 (relay gone → customer still born, trail says so), FT-03 (Identity restart → tokens survive) — see §10 | SQL Server outage not injected |
| R-10 Export opens as a formula in a spreadsheet | 3×3 = 9 medium | FND-04 fixed in this tree (`csvCell`), spec added | — |
| R-11 Open redirect at sign-in or language switch | 4×2 = 8 medium | SEC-02: `returnUrl` variants and `?lang=` probes stay local (audit A01-001/002 fixed) | — |
| R-12 Security headers missing on a host | 3×2 = 6 medium | SEC-01: CSP (nonce), X-Frame-Options DENY, nosniff, Referrer-Policy, Permissions-Policy, COOP, CORP on both BFFs and the sign-in page; HSTS only outside Development (by design) | Production headers unobserved |

## 6. Environment and data (playbook §7)

- Candidate identity: see the head of this document. The working tree is **uncommitted**; a later commit must be
  re-identified before any evidence is reused (gate 11).
- Isolation: fixtures carry the run's stamp (`qapetraHHMMSS`, `qaraceHHMMSS`…); the hands database is dropped and
  reseeded by `run-stack.ps1`, so the fixtures live until the next restart. The regression runner gives every suite its
  own database (`HarmonyIdentity_<suite>_PB1`).
- Authentication stayed real everywhere: page sign-ins through Identity's own pages (`s44\page-sign-in.ps1`), the
  BFF clients' key-based client authentication, DPoP proofs on every API call. No bypass.
- Observability: Identity's log (`api-s77\pages.log`), the BFF logs (`%TEMP%\hands-*.log`), the trail in the database.
  The log at 11278–11299 holds the stack trace of FND-01.
- Data dimensions exercised: empty → one → several records (fixtures), Unicode and Arabic names with combining marks
  and Arabic-Indic digits (PB-09), boundaries (300-char name, tier 9, negative rooms, page size 200), duplicates (codes),
  time windows (trail from/to, inverted window), permissions across three roles, history (restored database).
- **Environment hygiene finding (FND-06):** the local SQL Server holds about 600 `HarmonyIdentity_*` databases left by
  earlier suite runs; the runner adds 43 more per regression. Not a product defect; a clean-up job for the owner.

## 7. Manual catalogue (playbook §9) — what was executed and how

Legend: PB = `s72\tests\e2e\playbook-e2e.ps1` (API + observer), UI = hands-on in the browser pane (Chrome 156,
1366×768 unless said), FT = `s72\faults.ps1`, RS = `s72\restore.ps1`, SEC = curl probes, LOAD = `s72\load.ps1`.

| Family | Executed | Result |
| --- | --- | --- |
| M01 Screens | Deep links (tenant page by URL), reload, 404 route, browser back after sign-out, every console place through s71 and the gate; list/detail consistency through PB-19..23 | UI-06 `/no-such-place` → "This page does not exist"; UI-08 after sign-out `/tenants` → sign-in page; all s71/gate pages render |
| M02 Forms and input rules | Through the API: empty code + bad e-mail (400, 6 field codes), code with a space (400), tier 9 (400), reused code (202 00071), unknown country (202 00058), 300-char name (400 10505), negative rooms (400), unknown city (202 00055), duplicate hotel code (202 00091), unknown tenant (202 00070), Unicode round-trip; nothing written (PB-07..09). The wizard's client validation: s56 gate | Passed |
| M03 Lists | Paging without overlap and with a stable total, unknown sort member 400 10003 (PB-23), facets and search through s71/s70, page size cap 1–100 (10002) | Passed |
| M04 Roles and tenant isolation | PB-10..12 across hotel staff, a tenant administrator and a platform person; s53, s58, s70 | Passed |
| M05 Authentication and session | PB-13/14 (scheme, proof, tamper, replay), PB-04b (mailed link used once), UI-08 (sign-out ends the BFF session: `/bff/user` 401, next visit asks to sign in again), s14, s17, s18, s31, s33, s35, s37, s43, s45, s69 | Passed except **FND-01** (tampered token → 500 in Development) |
| M06 Workflow and lifecycle | Tenant birth → invitation → password from the link → first sign-in with two-step set up → suspend → reinstate (PB-01..04b, PB-18); repeated commands answer the same way (idempotent, no second trail row) | Passed; observation O-01 |
| M07 Concurrency and idempotency | PB-15 (receipt replay + mismatch), PB-16 (race on a code), PB-17 (suspend vs reinstate race) | Passed |
| M08 Calculations | Not applicable to Identity (no money); the licensed-rooms sum is reconciled in PB-20 | Not applicable / passed |
| M09 Data integrity and retention | Uniqueness (codes), relations (tenant → account → role), trail completeness (PB-03, 21, 25), restore reconciliation (RS-03). Retention rules: none defined (open requirement) | Passed; retention open |
| M10 Files and exports | CSV exports (tenants, properties, groups, people): quoting spec; **FND-04** formula injection fixed and specified; downloads not exercised in a browser | Fixed |
| M11 Integrations, jobs, notifications | Invitation mail (PB-04), relay outage and re-send (FT-02), two-step mails (s18, s20), outbox (s60, own settings) | See §10 |
| M12 Usability and accessibility | UI-01 skip link is the first Tab stop and lands in `main`; UI-02 row menu opens with Enter, lists Open/Edit/Suspend/Invite administrator/Properties, Escape closes it and focus returns to the opener (**O-02**: focus does not move into the menu on keyboard open); UI-03 Edit drawer takes focus (first field), Escape closes it, focus returns to Edit, no unsaved prompt when nothing changed; UI-05 at 683 px (200 % of 1366) the page reflows (rail hidden, bottom bar), no page-level horizontal scroll, the table scrolls inside its own wrap | Passed with O-02; no screen reader, no contrast tool (D31 known), no axe |
| M13 Platforms and localization | UI-04 Arabic: `lang=ar`, `dir=rtl`, Arabic headings, lead with Arabic plurals ("9 عملاء · 9 نشط"), dates "30 أكتوبر 2026", pager "10 صفوف · صفحة 1 من 1", scripts from `/ar/`; s71/s56 Arabic and RTL checks; dark theme (s71). Browsers: Chrome only | Passed; **Not run**: Firefox, Safari, Edge, real devices, IME |
| M14 Native / offline | Not applicable (web only) | — |
| M15 Operations | Trail as support reads it (PB-25), restore rehearsal (RS), Identity restart (FT-03), configuration review (settings keys, §1). Runbooks: none written yet | See §10; runbooks open |
| M16 Database-backed correctness | Every mutation above observed through the read-only observer: create (PB-01/02/06), rejected actions leave no row (PB-07/08), retry/duplicate (PB-15/16), concurrent (PB-17), forbidden effects (PB-05), reconciliations (PB-19..22), restore (RS-03) | Passed |

Exploratory charters executed (60–90 min each, folded into the above): interrupted onboarding (relay down, FT-02);
privilege boundaries from three seats (PB-10..12); the oldest data (restored database, RS); operator recovery
(FT-03). Not executed: resource contention between tenants, exports under restricted access.

### 7.1 The control-by-control UI walk (the user's addition: "every button, filter, field and form")

Every control of both apps was inventoried from the templates (`s72\ui-inventory.md`: 580 distinct controls on 28
screens) and then pressed, filled or opened by hand in the browser pane, as the platform person (console) and as a
hotel administrator (hotel app, Lina): 267 control rows recorded in `s72\ui-walk-results.md` with the
observed outcome of each — 221 passed, 12 failed (defects FND-09..19 below), 18
observations, 16 not reachable with the seed people (hotel Settings needs an administrator with View settings;
Amal's account has two-step). Then every UI defect and observation was fixed in the working tree (the "Fixes made"
table at the end of that file, 30 items), the two apps were rebuilt into the stack and each fix was pressed again.

### 7.2 The real-interaction pass of the console (the user's second addition: "click and write and test every thing")

The user then saw the console's filters "not working". The earlier walk had driven the controls with scripted DOM
events, which cannot see where a native popover is painted. So the whole console was walked a second time with the
browser pane's real mouse and keyboard (clicks, typed keystrokes, Enter, Escape, Ctrl+K), in two tabs, in English and
in Arabic, at 1366×768: every list facet, search box, pager, sort button, row menu, drawer form (empty submit, wrong
values, typed values, save), confirmation, tab, shell menu, theme, density, language switch and the five-step wizard
to a real tenant with a hotel, a module line and a mailed invitation. The record is the last section of
`s72\ui-walk-results.md`. It found three more defects (FND-21..23) and four more observations (O-33..36), all fixed
in the working tree, rebuilt and verified again on the stack; and it confirmed the earlier fixes under real input.

## 8. Automation (playbook §10–11)

| Layer | What exists | Run on this candidate |
| --- | --- | --- |
| Static | `run check`: ESLint, Prettier, translations (2803 units), architecture (37 libraries, 0 boundary violations, 0 cycles), styles (360 files), dependencies (20 packages), error codes (208 worded); the ts-patterns hook | ESLint 0 · Prettier clean · translations 2978 units / 0 problems · architecture 37 libraries / 0 boundary violations / 0 cycles · styles 360 files / 0 · dependencies 20 / 0 · error codes 252 worded, all declared (72 framework codes still without wording) |
| Unit / component (frontend) | Vitest + TestBed: libraries and the two apps | libraries 94 files / 345 tests; console app 12 files / 29 tests; all green on the final tree |
| Backend unit tests | **None exist** (no test project under `Backend/Services/Identity`) — gap for the owner | — |
| API / connected e2e | 43 PowerShell suites in `Harmony.TestEnv\s*` (real Identity, real BFFs, real database, page sign-ins), runner `s35\run-regression.ps1` | 43 suites on api-s77 before the fixes (tag PB1): 42 reported, 1006 passed, 4 failed at first attempt — s55 ×2 (a wording the suite still expected; suite corrected), s45 ×1 (the working-tree sign-in script's line endings; normalised), s30 ×1 ([1.4c] a made-up logoutId: "still signed in" false; unexplained); s37 threw once on a SQL timeout. The reruns after the fixes were not run (the stack must stop for them); the fixes are covered by the unit suites, the playbook suite (26/27 on the rebuilt stack) and the hands-on checks of every fix |
| Database reconciliation | `s72\tests\e2e\playbook-e2e.ps1` (read-only observer, versioned queries) | PB: 26 / 27 on the rebuilt stack (the one failure is PB-13 = FND-01, the framework's Development-only 500) |
| Visual | Screenshot audit against the Figma renders (redesign record §6.3b); no pixel-diff tool | Done by eye |
| Accessibility | None automated (the approved `@axe-core/playwright` is not installed) | **Not run** |
| Security | Dependency audits (NuGet ×2, npm), header and redirect probes, the OWASP audit of 22 Oct | §10 |
| Performance | `s72\load.ps1` | §9 |
| Operational | `s72\restore.ps1`, `s72\faults.ps1`, `run-stack.ps1` (fresh install: drop + migrate + seed) | §10 |
| CI | `.github/workflows/frontend.yml` is an uncommitted draft; **no pipeline runs** | Gate 10 open |

Automation quality notes: the suites assert stored effects (observer) and absences (leftover counts), create stamped
data, keep failure evidence (logs, JSON, screenshots), and record first-attempt failures; three suite runs were needed
to get the playbook suite right — the two earlier runs are kept in `s72\evidence` and their test-side corrections are
listed in §11 as *not defects*.

## 9. Performance (playbook §12)

`s72\load.ps1`, 10 threads for 60 s, every request a bound (DPoP) call to Identity on this laptop, with the test driver,
SQL Server, Redis and the browser on the same machine:

| Operation | Requests | 200 | p50 | p95 | p99 | max |
| --- | --- | --- | --- | --- | --- | --- |
| GET Tenant/summary | 5,797 | 5,797 | 59 ms | 74 ms | 95 ms | 545 ms |
| POST Tenant/get-all (page of 25) | 1,952 | 1,952 | 62 ms | 75 ms | 88 ms | 208 ms |
| POST SecurityAudit/get-all (page of 25) | 1,890 | 1,890 | 59 ms | 72 ms | 82 ms | 179 ms |
| **All** | **9,639 (79.7/s)** | **9,639, 0 failed** | | | | |

Offered = admitted = completed: closed-loop threads, no think time, so the rate is what ten tight loops could push, not
an arrival model. Percentiles are over all samples of a request type (successful and failed, none failed), measured
with `Stopwatch` around each call. Identity's own CPU and memory were not captured (the process is hosted by `dotnet`,
the script looked for the publish's exe name — corrected for the fault run).

A 24–72 h soak, peak and spike shapes, and a production-like dataset were **not run**: no agreed workload, no
production-like host, a laptop sharing CPU with the test driver. The numbers above characterise this machine, not
capacity.

## 10. Security, resilience, recovery (playbook §13)

**Headers and redirects (SEC-01, SEC-02).** Both BFF hosts answer with `Content-Security-Policy` (default-src 'self',
nonce for scripts and styles, frame-ancestors 'none', base-uri 'none'), `X-Frame-Options: DENY`,
`X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Permissions-Policy`, `Cross-Origin-Opener-Policy:
same-origin`, `Cross-Origin-Resource-Policy: same-origin`; unauthenticated API answers carry `default-src 'none'`. The
Identity sign-in page: CSP with nonce, `form-action` limited to self and the hotel app, `__Host-` cookies (device, lax,
400 days; antiforgery, strict). HSTS is sent only outside Development (code at `Program.cs:248`), so it is unobserved
here. `/bff/login?returnUrl=` with `https://evil.example/`, `//evil.example/`, `/\evil.example/` and `%2F%2Fevil` all
start a normal sign-in (the stored return path falls back to `/` by `LocalPathOnly`); `?lang=//evil.example` and
`?lang=%2F%5Cevil.example` answer 200 without a redirect (the 22 Oct audit's A01-001/002 are fixed).

**Dependencies (SEC-03).** `dotnet list package --vulnerable --include-transitive`: Identity and the BFF have no known
vulnerable package (sources nuget.org and the local framework feed). `pnpm audit --prod`: none. `pnpm audit` (dev):
6 advisories, all in `vite` 8 (launch-editor and dev-server file-serving issues, patched in ≥ 8.3.3) — **FND-05**, build
tooling only.

**Faults (FT, `s72\faults.ps1`; local containers and the local Identity process only, each undone in a `finally`).**

| Fault | Blast radius / duration | What stayed true | What did not | Result |
| --- | --- | --- | --- | --- |
| FT-01 Redis stopped | one container, 46 s | Sign-in through the pages still worked (a token was issued during the outage); once Redis was back every call answered 200 in 50–78 ms | Every bound API call during the outage waited ~6 s and then answered **401** (five reads, one list) — the proof or token check needs the store and fails closed, and the client would read it as "signed out" | **FND-07** |
| FT-02 mail relay stopped during an onboarding | one container, ~10 s | Tenant/Create answered 201 in 4.4 s, the customer row reads back, the trail wrote `tenant.administrator-invitation-failed` beside `tenant.created` and `account.created` | The first run's re-send call used the wrong verb (test-side, 405); passed on the rerun (the first attempt used the wrong verb for the re-send) | Tenant/Create 201 in 4.2 s, the row readable, the trail wrote account.created and tenant.administrator-invitation-failed; after the relay returned, the re-sent invitation reached the administrator's inbox |
| FT-03 Identity killed under a live token | one process, 13.2 s gap | While down the door refused the connection outright; after `dotnet Harmony.Identity.Api.dll` was started again the token issued before the kill still worked (server-side sessions and persisted keys) and a new sign-in worked | — | Passed |

**Restore (RS, `s72\restore.ps1`).** Copy-only checksummed backup 1.8 s (1.1 MB); restore under another name 2.1 s;
20 business tables identical key for key in both directions (volatile tables compared by count: no difference at the
moment of the run); a second Identity started on the restored database in 13.7 s; the platform person signed in against
it and read the same customer summary as the live one (9 active, 0 suspended). **RTO measured 15.8 s** on this machine
for this size; RPO is the backup's age (no log shipping). The restored database and the second Identity were removed.

**Privacy.** Passwords and tokens never appear in the evidence (the sheet values are read at run time and not printed);
the observer sees personal data (e-mails, names) — evidence files stay in the test environment folder, not the repo.
No retention rule exists for the trail or sessions (open requirement, also redesign decision 12).

## 11. Defects and observations (playbook §15)

| Id | Severity | Where | What | Status |
| --- | --- | --- | --- | --- |
| FND-01 | S3 (Development only; would be S2 if details were on in production) | Framework `Harmony.Core.Identity` `PlatformAuthenticationConfigurator.AnswerUnauthorized` | A token with a bad signature makes Identity answer **500** instead of 401: the JWT failure message (with line breaks) is written into `WWW-Authenticate` and Kestrel refuses the header (`Invalid non-ASCII or control character in header: 0x000A`, log 11278). `IncludeErrorDetails` is on only in Development, so production answers 401. Fix: strip control characters from the detail before building the header. Repro: PB-13 `tampered=500`. | **Open — framework repository** |
| FND-02 | S3 | Identity, invitation acceptance | After the administrator sets the password from the mailed link (proof of the address), `UserAccounts.EmailConfirmed` stays 0. Nothing enforces it today; it misstates the record. | Open — owner |
| FND-03 | **S1** | Both apps + sign-in page | The sign-in page's **Forgot password** link points at `/forgot-password` on the app's host; neither app has that route — the page is "This page does not exist". Identity has the doors (`ForgotPassword`, `ResetPassword`, rate-limited) but no page uses them. A person who forgot the password has no path. | **Open — blocks release** |
| FND-04 | S2 | Frontend `libs/shared/domain/csv.ts` | CSV exports wrote cells starting with `=`, `+`, `-`, `@`, tab or CR as typed; a hotel name chosen by a tenant administrator could run as a formula in a platform person's spreadsheet. Fixed: such text cells get a leading apostrophe; numbers pass; spec extended. | **Fixed in this tree** |
| FND-05 | S3 | Frontend dev dependencies | 6 advisories in `vite` 8 (dev server only), patched in ≥ 8.3.3; the Angular build tooling pins the version. | Open — owner (upgrade when the toolchain allows) |
| FND-06 | S3 | Test environment | ~600 `HarmonyIdentity_*` databases on the local SQL Server from past suite runs; the runner adds 43 per regression. | Open — owner |
| FND-07 | S2 | Identity (token/proof validation with Redis) | With Redis unavailable, every bound API call waits ~6 s and answers **401** (FT-01); sign-in pages keep working; recovery is immediate when Redis returns. Failing closed on a missing replay store is defensible, but the answer should be a fast 503 (service unavailable) rather than a slow 401 that signs the person out, and the dependency should be named in the health page. | Open — owner |
| FND-09 | S2 | Console wizard, hotel step | The four regime selects were required but had no message: Next did nothing. | **Fixed**: the message shows under the selects once Next was pressed |
| FND-10 | S2 | Console tenant page, Invite administrator | Both fields empty (the re-send the drawer promises) sent nothing and the drawer stayed; one field typed demanded both. | **Fixed**: empty = re-send; one filled asks for the other |
| FND-11 | S2 | Identity `UpdatePropertyCommandHandler` | The platform person could not save a customer's hotel: the update looked the hotel up inside the caller's own tenant ("That property no longer exists."). | **Fixed** (the platform finds it across tenants), verified on the rebuilt stack |
| FND-12 | S3 | Frontend wording | The group editor's codes 10701–10719 and the reference codes 10416–10429 had no wording: every message read "Some fields need attention." | **Fixed**: 33 codes worded in both languages |
| FND-13 | S3 | Reference row editor | Every required field that is not the key asked "Give the name in English." — the Arabic name included. | **Fixed**: each field carries its own code (`requiredCode`) |
| FND-14 | S3 | Lookup values | A new value was born inactive and vanished from the list right after "Lookup value added." | **Fixed**: a new row is active unless retired |
| FND-15 | S3 | Identity group list | The list answer carried `brand: null`, so the Agreement column and the export were empty for a brand with dates. | **Fixed** (the list projection carries the brand), verified on the rebuilt stack |
| FND-16 | S3, intermittent | `libs/ui/overlays` | A drawer or a confirmation opened right after another one was cancelled came up empty, or its confirm did nothing (2 of 4 attempts; the invitation cancel once). | Guarded: a native close that lands while the dialog already shows the next thing is ignored; the root cause is to confirm |
| FND-18 | S3 | Console rules page | The switch's save and the days' save in flight together ended in 202 `ConcurrencyConflict`, worded as the generic refusal; the 202 body carries the exception's stack trace (Development). | Wording **fixed** (a conflict reads as a conflict); the race and the Development detail stay with the owner |
| FND-19 | S2 | Console Sign-ins page, Your sessions | Sign out sent the device without the account id: 400, "Which account? Reload the page and try again." | **Fixed**: the signed-in person's id goes with the device |
| FND-21 | **S2** | Every list facet (`libs/ui/data/facet`) | The facet popover painted at the viewport's bottom-left: a native popover lives in the top layer, so CSS insets relative to the chip meant nothing. Scripted events never showed it; the user did ("filters not working"). | **Fixed**: placed by `PopAt` like the row menus; verified in LTR and RTL on six list pages |
| FND-22 | S2 | Tenant page and property page, subscription lines | Identity's `isHeld` ("in force today") was read as "on hold": a line in force read "Held" with a warning tone and a line starting tomorrow read "Active". | **Fixed**: Active · Ends d · Starts d · Not in force · Disabled; wording in both languages |
| FND-23 | S2 | Property editor and group editor | The tenant select read the Tenants page's filtered store: with the list narrowed to one tier, the New property drawer offered 2 of 7 customers. | **Fixed**: both editors read the unfiltered customer list the facets use (platform tenant excluded) |
| O-33..O-36 | Observations | Console | Letters in Rooms read "out of range" (O-33); focus fell to the top of the page after a confirmed deletion (O-34); reference choices in English inside the Arabic editors (O-35); agreement dates as ISO strings (O-36); plus the row editor landing on its Close button, the tenant editor on its locked code, the lookup value asking for the category again, Escape needing two presses in the place search, menus not following a window resize, three forms without `aria-invalid`. | All **fixed** and verified on the rebuilt stack |
| O-01 | Observation | Identity lifecycle | Suspending a suspended customer and reinstating an active one answer 200 again (idempotent) rather than a refusal; the state stays right and the trail does not gain a second row. Fine as a contract; worth stating in the API docs. | Owner |
| O-02 | Observation | Console row menus | Opened with the keyboard, the menu lists its actions but focus stays on the opener; Escape closes and keeps focus there. WCAG allows it; the ARIA menu pattern moves focus to the first item. | Owner |
| O-03 | Observation | Trail semantics | Platform actions on a customer (`tenant.created`, `tenant.suspended`…) are filed under the platform tenant with the customer named in the detail; the customer's own tenant filter shows what happened inside it (accounts, sign-ins). The search by code finds both. | Documented |

| O-04..O-32 | Observations | Both apps | The walk's observations, each recorded with its screen in `s72\ui-walk-results.md`: silent actions without a toast (O-01 family), the platform row offered Suspend (O-06), empty-select wordings that pointed at a reload or a format (O-07), a module line accepting a non-number silently (O-10), Add module replacing a line without a word (O-11), stale hotels table / head counts / home trail / people list after a change (O-12, O-15, O-24, O-27), Clear leaving "Include inactive" on (O-20), empty states without a word (O-16, O-22, O-26, O-28), Export shown on the Invitations tab (O-29), a search button promising entities while finding places (O-32), a refused role change leaving the box ticked (O-25), 12-hour times on the trail card (O-13). | All **fixed** except the owner's calls: O-03 (what the trail files under the customer), O-05 (Cancel discards without asking), O-14 (facets are not in the URL), O-18 (the System column marks system rows only, by design), O-19 (the row editor's "last changed by" is an account id: Identity sends no name), O-23 (the audit export is the page), O-30 (Send again's toast, to confirm) |

FND-08 was not used; FND-17 (the invitation cancel that did nothing) turned out to be FND-16's race and was merged into it.

Not defects (test-side corrections made during the campaign, kept honest): the people list's page size cap (100) caught
my 200; the invited administrator's flags differ from my first assumption (recorded as FND-02 instead of asserted); the
trail's tenant column semantics (O-03); a PowerShell array-unwrapping slip in the observer helper.

## 12. Metrics (playbook §16)

| Metric | Value (numerator / denominator, window) |
| --- | --- |
| Scope disposition | 12 of 13 console places and both hotel-app areas Covered; 1 Gap (forgot password); 4 infrastructure items Not run (deployment, migration ordering, certificates, SQL failover) |
| Mandatory release scenarios | 14 of the 16 families executed and passed (M08 calculations and M14 native are not applicable); the password-recovery path found missing under M05/M06 is now built and walked end to end |
| Playbook suite (PB) | 26 / 27 on the rebuilt stack (the one failure is PB-13 = FND-01, the framework's Development-only 500) first-attempt on the final script; earlier attempts 18/27 and 0/1 were test-side |
| Fault injection (FT) | 3 of 3 run: FT-01 Redis stopped (FND-07 observed), FT-02 mail relay stopped (passed on the rerun), FT-03 Identity killed (recovered) |
| Restore (RS) | 6 / 6 |
| Hands-on (UI) | the control-by-control walk: 267 control rows, 221 passed, 12 failed, 18 observations, 16 not reachable (§7.1); the real-interaction pass of the console: 14 screens, every control pressed or typed into, 3 defects and 4 observations found and fixed, every earlier fix confirmed under real input (§7.2) |
| Security probes (SEC) | 4 / 4 as expected |
| Reconciliation PB-19 | Tenant/summary vs `Tenants` (customers = rows except the platform tenant): active 9/9, suspended 0/0, terminated 0/0, countries 1/1, onboarded this month 9/9 — census, exact |
| Reconciliation PB-20 | Property/summary vs `Properties` + `TenantSubscriptions`: total 3/3, active 3/3, opening soon 0/0, created this month 3/3, declared rooms 280/280, licensed rooms (enabled, current lines of active hotels) 200/200 — census, exact |
| Reconciliation PB-21 | SecurityAudit/counts last 24 h vs `SecurityAuditEvents` in the same window: 68/68 over 14 actions — census, exact |
| Reconciliation PB-22 | People list total vs platform tenant accounts: 2/2 |
| Reconciliation RS-03 | 20 business tables, 0 missing, 0 extra keys; 53 other tables equal by count at the moment of the run |
| Defects found / fixed / open | 22 found (FND-01..23 less the two unused numbers; FND-21..23 from the real-interaction pass, all fixed) · fixed in the tree: FND-03, 04, 09, 10, 11, 12, 13, 14, 15, 16 (guard), 18 (wording), 19 · open for the owner: S2 FND-07; S3 FND-01 (framework), 02, 05, 06 |
| Automation health | PB first-attempt failures on the final script: 1 (PB-13 = FND-01); the earlier 25/27 also carried PB-11, a suite-side assumption since corrected; regression first-attempt: 4 checks of 1010 and one suite throw, each explained in §8; no quarantines |

Verification coverage caveat: the reconciliations are a census of this small seed (10 tenants, 3 hotels, 68 trail
rows); exactness here says the formulas agree, not that they would at production volume.

### 12.1 Measurement specifications (playbook §16.2, one row each)

| Metric | Grain and population | Oracle (independent of the API) | Collection and freshness | Comparison | Threshold |
| --- | --- | --- | --- | --- | --- |
| PB-19 customer counts | One row per customer: `Tenants` rows except the platform tenant (`Id <> PlatformTenantId`) | `GROUP BY Status`, `COUNT(DISTINCT CountryCode)` of the active ones, onboardings since the 1st of the month (UTC) | Observer query right after the API read, primary database, no concurrent writer | Exact integers | 0 difference |
| PB-20 hotel counts and rooms | One row per hotel (`Properties`); licensed rooms = `SUM(Rooms)` of enabled `TenantSubscriptions` whose period covers today (UTC) and whose hotel is active | Arithmetic over the observer's rows | Same | Exact integers | 0 difference |
| PB-21 trail counts | One row per `SecurityAuditEvents` row with `OccurredAtUtc` in [now−24 h, now+1 min) | `COUNT(*)` in the same window, both ends UTC | Same second | Exact integer | 0 difference |
| PB-22 platform people | One row per `UserAccounts` row of the platform tenant | `COUNT(*)` | Same | Exact integer | 0 difference |
| RS-03 restore completeness | One row per business-table key (20 tables) | The live database at the moment after the backup | Keys compared in both directions; volatile tables by count | 0 missing, 0 extra | 0 |

The oracle for the lossless-storage checks (PB-01, PB-02, PB-06, PB-09) is the request body the suite sent; agreement
there says the service stored what it was given, not that the business facts are true (playbook §16.1).

## 13. Release gates and decision (playbook §17)

| Gate | Status | Why |
| --- | --- | --- |
| 1 Scope accounted for | Met for the application; infrastructure gaps named | §4 |
| 2 Business correctness | Met with reservations: regression 1006 of 1010 first-attempt checks with known causes, PB 26/27 (the one failure is the framework's Development-only 500), every walked control passes after the fixes | Regression + PB |
| 3 Defects | Met for S1 (FND-03 fixed: the three password pages exist in both apps); S2 open: FND-07 | §11 |
| 4 Security | Met for the exercised scope; **register incomplete** | No ASVS requirement-ID register (gate 15); the 22 Oct audit's configuration findings (secrets in versioned settings, proxy trust) stay with the owner |
| 5 Data integrity | Met | PB-01..23, RS-03 |
| 6 Performance and stability | **Not met** | No agreed thresholds, no soak, laptop measurement only |
| 7 Recovery | Partly | Restore rehearsed locally (15.8 s); no RTO/RPO agreed, no off-site backup, no failover |
| 8 Experience | Partly | every control of both apps pressed (§7.1), then the console again with real clicks and typing in both languages (§7.2), and the findings fixed; Chrome only; Arabic and reflow checked; no screen reader, no automated accessibility scan |
| 9 Business acceptance | **Not met** | The owner has not reviewed this candidate |
| 10 Automation integrity | Partly | Suites green locally; CI does not run |
| 11 Candidate validity | Met for this tree; **re-identify after commit** | 171 uncommitted paths |
| 12 Operations | **Not met** | No deployment target, runbooks, alerting |
| 13 Decision | Recorded below | — |
| 14 Measurement validity | Met | Denominators, census vs sample stated |
| 15 Standards claims | None made | — |
| 16 Observation safety | Met | Read-only observer proven; faults on local containers only |

**Recommendation: No-go for production as it stands; Go for the owner's review of the console work and the
fixes.** What blocks production is not what this campaign found in the two apps or the API — what was found there is
fixed in the working tree (FND-03 included) — but the surrounding programme: FND-07 (a cache outage reads as "signed
out"), business acceptance, agreed performance and recovery objectives, a running CI, an operations plan, and the
configuration findings of the 22 Oct audit. Everything else found here is S3 or fixed.

## 14. What this campaign leaves behind (playbook §19)

- This record (`docs/qa/platform-qa-campaign-2026-10-30.md`).
- `D:\PMS System\Harmony.TestEnv\s72\`: `observer-setup.sql`, `observer-fix.sql`, `tests\e2e\playbook-e2e.ps1`,
  `faults.ps1`, `load.ps1`, `restore.ps1`, `evidence\*` (JSON with every observer query and result, text results).
- `s35\regression-PB1\summary.txt` and one log per suite.
- `s72\ui-inventory.md` / `.json` (every control from the templates) and `s72\ui-walk-results.md` (every control's
  observed outcome, screen by screen, and the "Fixes made" table).
- The fixes in the working tree (uncommitted, with the console batches): the password pages
  (`libs/platform/session`), the wording and reference-field codes, the overlays' close guard, the device sign-out,
  the conflict wording, the stale-list reloads, the empty-state hints, the toasts, and the two Identity changes
  (`UpdatePropertyCommandHandler`, `PropertyGroupProjections`); the Development mail links now point at the dev host;
  from the real-interaction pass: the facet placement (`libs/ui/data/facet` on `PopAt`), the line-state wording, the
  editors' tenant list, the drawer/confirm focus rules, the place-search Escape, the lookup-value default, the
  agreement dates, `aria-invalid` on three forms.
- Owner decisions raised: FND-01 (framework), FND-02, FND-05, FND-06, FND-07, FND-18's race, O-03, O-05, O-14, O-19,
  O-23; the pending D20/D29/D31;
  retention rules; a backend test project; CI; the environment clean-up.
