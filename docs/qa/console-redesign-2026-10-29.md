# Platform console redesign — QA record (29–30 Oct 2026)

The platform console (`apps/harmony-console`) was rebuilt to the Figma file *Harmony Platform Console*
(`RMIE3aJj2QXdLwSlHg9xu9`, pages 10 Foundations, 11 Components, 20 Screens EN, 21 Screens AR, 22 Responsive).
Everything here is **uncommitted** and waits for the owner's review; nothing was pushed.

## 1. What was built

| Area | Where | Figma |
| --- | --- | --- |
| Console tokens (light + dark), laid over the hotel tokens for the console only | `libs/ui/tokens/console.css` | 10 Foundations |
| Console shell: 48 px top bar (lockup, Ctrl K search, environment · ring chip, bell, help, account), 240 px rail in three sections folding to a 56 px icon rail, phone bottom bar, the strips of the hotel shell | `libs/ui/console-shell/` | Top bar 4:27, Side nav 4:68, 22 Responsive |
| Console page language: page head, cards, KPI cards, status chips, filters, tables, tabs, steps, message bars, skeletons, empty states, drawers | `libs/ui/console-shell/console-ui.css` | 11 Components |
| Shared page head: title, lead, trail of sections, the page's actions beside the title in the console (the hotel app keeps its context bar) | `libs/ui/shell/page-head.ts` (`PAGE_ACTIONS_IN_HEAD`) | 20 Screens |
| Home: greeting, Needs you now, four numbers, new tenants per month, platform health, recent trail | `apps/harmony-console/src/app/home/`, `attention-store.ts`, `attention-bridge.ts` | C1 6:6 |
| Audit log with filters (window, actor, tenant, event family), drawer with every fact Identity keeps, Copy JSON, Export CSV | `libs/domains/platform-operations/feature-trail` | C8 16:2082 |
| Sign-ins and devices: four figures against the day before, outcome · tenant · method filters, your sessions, platform sign-in policy | `libs/domains/platform-operations/feature-sign-ins` | C9 18:2319 |
| Platform health: services map (14 services of `docs/services-map.html`), messaging checks, releases and rings with flags, health checks, live updates with a pause | `libs/domains/platform-operations/feature-health` | C10 19:2565 |
| Settings: the settings registry page mounted at `/settings` | `administration/feature-settings` | C11 (see §5) |
| Older pages (tenants, properties, groups, reference, people, roles, invitations, company login, directory, approvals, settings) in the new frame: page head with actions beside the title, filter row, table in a card, chips | converter over 11 templates + `console-ui.css` | C2–C7, C14–C23 |
| Onboarding in the rail opens the tenant creator (`/tenants?create=1`) | `app.routes.ts`, `tenants-page.ts` | C5 (see §5) |

Doors added so that every number on screen is real:

| Door | Who | What |
| --- | --- | --- |
| `POST /api/SecurityAudit/get-all` | ViewTenants, platform tenant only (00073) | the trail across every customer, paged, newest first; filters: search, action or family prefix, a list of actions, tenant, person, actor (a platform person's account or `system`), window; names of the customer, the person and the actor |
| `GET /api/SecurityAudit/counts` | same | how often each action was written in a window (default: the last 24 hours) |
| `GET /api/Tenant/summary` | same | active / suspended / terminated, countries, onboarded this month and by month (12), suspended customers, fixed-term lines ending within 30 days |
| `GET /api/Property/summary` | same | hotels, active, opening soon, created this month, rooms declared vs licensed today, hotels above licence |
| `GET /bff/health` (console BFF) | platform people (404 otherwise, 401 without a session) | Identity's detailed health checks read inside the network with the ApiDocs pair; a 502 still carries a report with the word *Unreachable* |

Design docs: `docs/identity-aggregates.html` §4.4 names the read doors.

## 2. Automated checks (all green at the time of writing)

| Check | Result |
| --- | --- |
| `corepack pnpm@12.14.0 run check` (eslint, prettier, translations, architecture, styles, dependencies, error codes) | 0 problems; 2 327 translation units, 36 libraries, 0 boundary violations |
| `ng test harmony-web` (every library) | 86 files, 308 tests passed |
| `ng test harmony-console` | 4 files, 9 tests passed |
| `ng build harmony-console --configuration production,en` / `,ar` | initial 478.8 kB (budget 500 kB), no warnings |
| `ng build harmony-web --configuration production,en` / `,ar` | initial 429.3 kB, no warnings |
| Identity publish (`s65/rebuild.ps1 -Target api-s70`) and BFF build | green |
| e2e `s70/tests/e2e/platform-operations-e2e.ps1` (the four Identity doors, every filter including the actor, paging, sorting, 00073 for a customer's administrator, 401 anonymous) | 15 passed, 0 failed |
| e2e `s71/tests/e2e/console-e2e.ps1` (headless Chrome walk of the console: every new place, drawer and filter, bell, `/bff/health` and `/bff/flags` from inside the page, the older pages in the new frame, onboarding, Ctrl K, rail fold, dark theme, Arabic RTL, tablet and phone, sign-out; zero console errors and zero CSP / Trusted Types violations on every visit) | 15 passed, 0 failed (§3) |
| e2e `s56/tests/e2e/angular-page-gate.ps1` (the hotel app and the console, both languages, against `api-s70`) | 44 passed, 0 failed (§3) |

New unit specs: console shell, lockup, places, page head, trail / health / sign-in wording, clock, CSV, the five stores,
the three new pages, the home and the attention store.

## 3. Browser walk and hands-on QA

**Hands-on (29 Oct, the local stack `s56/run-stack.ps1 -Run api-s70`, signed in as the seeded platform person at
`https://localhost:5220`):**

- Home: live numbers (one active tenant, one hotel, licensed rooms 0 of 120 declared, the day's sign-ins), Needs you now,
  the twelve month bars, the Platform health card read from `/bff/health`, Recent trail; Refresh and New tenant work.
- Audit log: 24 events newest first; the window, tenant and family filters narrow the list; a row opens the drawer with
  its facts, Copy JSON and Open tenant; Export CSV saves a file.
- Sign-ins and devices: the four figures against the day before, rows with the method and the outcome chip, the
  person's own sessions with Sign out, the policy facts (each verified in the code: 15-minute access token, 12-hour
  refresh, BFF idle 60 min and 12 h absolute, 12-character passwords with a symbol, breached and context words refused,
  429 with Retry-After, DPoP).
- Platform health: Identity's checks, the BFF's ring and release, the infrastructure checks, the services map, live
  updates with Pause.
- Tenants with Add tenant beside the title; `/onboarding` opens the creator; Settings, Reference, People and Roles in
  the new frame.
- Arabic (`?lang=ar`): right to left, the rail groups المنصة · العمليات · الإدارة, the home and the audit log in Arabic.
- Every door answered 200; the only console error was the expected 401 before sign-in; 0 CSP violations.

**s71 `console-e2e.ps1` (headless Chrome against a fresh Identity, 30 Oct):** 15 passed, 0 failed. Its first run found
two wrong expectations, not product faults: the home check wanted one active tenant while the seed had only a suspended
customer (the summary leaves the platform's own row out), so the seed now makes an active customer too and the lead is
checked for *1 active tenant in 1 country*; and the drawer check wanted eight facts where Identity keeps seven, so it
now names the seven. The last run (after the actor filter) is 15 passed, 0 failed again; on the way it caught one real
fault and one harness gap: the Audit log's date box showed *Last 24 hours* while the page read seven days (the select's
value was set before its options existed, so the browser fell back to the first one; the chosen option now carries
`selected`, the spec and the suite read the box), and the suite now writes its own ring and flag into the console copy
instead of trusting whichever script made the copy last. Screenshots: `Harmony.TestEnv\s71\shots\console-*.png` (home,
home-folded, home-dark, home-ar, home-tablet, home-phone, trail, trail-drawer, trail-ar, sign-ins, health, health-phone,
tenants, people, settings-page, reference, onboarding).

**s56 `angular-page-gate.ps1` (the hotel app and the console in headless Chrome, both languages, against `api-s70`,
30 Oct):** 44 passed, 0 failed. The gate builds both apps itself; its console checks now count the twelve rail places
and read the action beside the title (`railLinks`, `phActs` in `s56/cdp-page.mjs`) where they used to count the old
dock and the context-bar slot, and it defaults to the `api-s70` Identity because the console's shell reads the new
doors on every page. Console screenshots: `Harmony.TestEnv\s56\shots\console-*.png` (tenant-new, tenant, property-new,
group-new, group, settings, reference, ar).

**Builds after the last change (30 Oct):** console 478.76 kB initial in both languages, hotel app 429.27 kB, no
warnings, both under the 500 kB budget.

## 4. Production-readiness notes

- Trusted Types and the per-request CSP are unchanged; the new pages add no inline styles, no `innerHTML`, no new
  third-party package, font or CDN. The chart is inline SVG; the CSV export is a Blob under a short-lived object URL.
- Every read goes through `hasValue()` before `value()` (an errored resource throws otherwise); a door that refuses
  leaves its card quiet and its page says why, with Try again.
- The bell's store loads as its own chunk after boot, so the first load stays under the 500 kB budget.
- WCAG: skip link, focus to the heading after navigation, `aria-current` on the rail and the bottom bar, the folded
  rail names its places by title and tooltip, every chip carries a word (never colour alone), live updates on the health
  page can be paused, targets ≥ 32 px, logical CSS only (RTL by markup), reduced motion respected by the shimmer.
- No personal data in URLs, storage or logs: the only hint kept on the device is the rail's folded state.

## 5. What the Figma shows that has nothing behind it (owner decisions)

Left out rather than faked. Each needs a backend decision first:

1. **Billing** (rail item, home "Payment" rows, "Open billing", tenant tabs) — no billing service exists.
2. **Customize** on the home — no layout store.
3. **Declare incident** / **Open telemetry** on Health — no incident flow; the collector endpoint is not a UI.
4. **Messaging counters** (outbox pending / sending / set aside, inbox received) — the framework exposes health checks,
   not counts; the Messaging card shows the messaging-related checks instead.
5. **Releases and rings management** ("Manage", switches) — the ring, release, floor and flags are read from `/bff/flags`
   and shown read-only.
6. **Settings C11 groups** (platform profile, security defaults with toggles and numbers, notifications, legal) — the
   settings registry (keys, values, where a value comes from) is what the service has. *Batch 2 built C11 on it: the
   registry's platform defaults grouped by owning service, edited in place (§6).*
7. **New tenant wizard C5** — *built in batch 2 at `/tenants/new`; Onboarding, the home and New tenant open it (§6).*
8. **Tenant list columns** Hotels, Rooms licensed · in use, Administrator — *built in batch 2: the `TenantCompany`
   describer carries them (§6).*
9. **Sign-in rows for unknown accounts / rate-limited addresses** — the trail is written per account; refusals without an
   account are not rows today.
10. **Audit log "Outcome" column and filter** — Identity writes one action per event and the outcome is part of its name
    (`sign-in.wrong-password`); there is no separate outcome field. The Sign-ins page derives the outcome from the action;
    the Audit log shows the action's words and tone instead of an Outcome column.
11. **Drawer facts Reason, Status (Active → Suspended), Request (path · Idempotency-Key), Correlation (traceparent) and
    the Related events list** — Identity keeps the actor, the tenant, the person, the address, the device, the time, the
    code and a detail text (the reason lives there). The other facts need the trail to carry the request's correlation id
    and the state before and after; the drawer shows the seven facts that exist.
12. **"kept 180 days online, archived 7 years" in the lead** — no retention rule exists for the trail; the lead says the
    window, the count and *every customer, in UTC*.

## 6. Batch 2 (30 Oct): every screen rebuilt to the Figma

The owner looked at the stack after batch 1 and said the screens were not the Figma: only the shell and the four new
pages had its layouts; the sign-in page and every older page had the frame and nothing more. Batch 2 rebuilt each of
them to its Figma frame, with real doors only. Everything is **uncommitted** again; nothing was pushed.

### 6.1 What was rebuilt

| Screen | Where | Figma | Notes |
| --- | --- | --- | --- |
| C12 / C12b sign-in and refusal pages (Identity's Razor pages for the console client) | `Harmony.Identity.Api/Pages/Account/Login.cshtml`, `Pages/Shared/_Layout.cshtml`, `_Lockup.cshtml`, `ConsoleFrame.cs`, `wwwroot/css/sign-in.css`, `wwwroot/img/console/*.svg`, `SignInPagesSettings` (`IdentitySettings:SignInPages`: PlatformContact, HelpUrl, PrivacyUrl, TermsUrl) | C12 21:2886, C12b | The split panel: lockup, lit door, "All sign-in services are running · HH:mm UTC", the two-step exception count, Identity's version; e-mail first, then the password; a hotel account is refused with its tenant's name, the web app's door and a reference the trail also carries |
| C2 Tenants list | `platform-admin/feature-tenants/tenants-page.*`, `tenants-store.ts`, Identity `GetPagedTenantsQuery` (+Statuses, Tier, CountryCode, ModuleCode) and `TenantCompany` describer | C2 | Lead counts from `/api/Tenant/summary`; facets Status · Tier · Country · Modules (`hr-facet`); columns Hotels, Rooms licensed · in use (▲ over), Modules, Onboarded, Administrator with the invitation's state; row menu with the real doors; Export CSV |
| C3 Tenant page | `tenant-page.*`, `subscription-editor.*` (C3b), `platform-admin/ui/trail-card.ts` | C3, C3b | Code tile, status and tier chips, tabs Overview · Hotels · Groups · Subscriptions · Company login · Directory · People · Trail; Administrator, Subscription and "How their people sign in" cards; the lines table; the trail card; Add module as the Figma's drawer (scope cards, renewal switch, the flag note, "What happens on Add") |
| C4 Properties list | `feature-properties/properties-page.*`, `properties-store.ts`, Identity `GetPagedPropertiesQuery` (+CountryCode, PropertyTypeCode, State, AboveLicenceOnly) | C4 | Lead from `/api/Property/summary`; facets Tenant · Country · State · Type and the "Above licence only" switch; columns Tenant, City · time zone, Type, Rooms licensed · in use, Modules, Opens · closes, State |
| C16 Property page | `feature-properties/property-page.*` (+spec) | C16 | Code tile, state and "N rooms above licence" chips, the licence message bar with Raise the line (opens the tenant's Add module aimed at the hotel), tabs Overview · Subscriptions · Contacts · Groups · Trail, Licence · Contacts · Groups cards, the lines table, the trail card; the edit drawer unchanged |
| C14 Groups list | `feature-groups/groups-page.*`, `groups-store.ts` (+isActive) | C14 58:3429 | Lead counts by type; facets Tenant · Type · State; columns Group (code · name, external id · franchisor or description), Tenant, Type chip, Hotels, Precedence, Agreement, State; Export CSV; row menu |
| C15 Group page | `feature-groups/group-page.*` (+spec) | C15 58:3829 | Code tile, state and type chips, lead "precedence · N hotels now, N past · agreement · royalty"; tabs Overview · Hotels · Brand · Standards · proposed; Brand, About and Standards (marked *Not built*) cards; hotels table with city, period and state, End per current member; Add hotel and the edit drawer unchanged |
| C17 People, C18 Person, C19 Invitations, C20 Approvals and rules, C21 Company login, C22 Company directory — console-only pages | `apps/harmony-console/src/app/people/*` (shared head + tabs, six pages, `stamps.ts`); Identity `GetPagedUserAccountsQuery` (+State, TwoStep, RoleId) and `AccountSignIns` describer (`LastSignInUtc`, `LastSignInDevice` on `UserAccountModel`); `UsersStore` census, filters, `everyone()`; `PeoplePage` takes `?role=` | C17 59:4031, C18 59:4484, C19 59:4894, C20 60:4670, C21 60:5007, C22 60:5314 | The hotel app keeps its own Users pages untouched: the console pages live in the console app and reuse the shared editors (`AccountCreator`, `AccountStateEditor`, `HotelAccessEditor`, `RoleAssignment`), which the `feature-users` index now exports |
| C7 Roles as a permission matrix | `apps/harmony-console/src/app/roles/*`; `feature-roles` exports `RoleEditor` and the wording | C7 15:1763 | One column per role (system roles read-only with ticks, custom roles ticked here), rows by area, "N changes not saved" with Discard / Save changes (one `UpdatePermissions` per changed role), the chosen role's people card with Assign people (→ `/people?role=`), New role / Edit / Delete unchanged |
| C11 Settings (platform scope) | `apps/harmony-console/src/app/settings/*` | C11 20:2783 | The settings registry grouped by owning service; each key's platform default edited in place (switch, number, time or text by its data type) and saved together through `ConfigSetting/Update`; Open settings registry → `/reference/settings` |
| C23 Reference data index, C6 reference table | `feature-reference-data/reference-index-page.*` (+spec), `reference-table-page.*` | C23 60:5652, C6 14:1519 | Tiles with live counts and a search, the still-empty message; the table page with the rail of the thirteen tables, search + facets, "Include inactive" (active rows only to start), "N of total", sorted-by in the footer, "Edit city · Muscat" with the row's last change in the drawer; the per-category table (Lookup values) says *per category* instead of a count |
| C5 New tenant wizard | `feature-tenants/new-tenant-page.*` (+spec), route `/tenants/new`, Onboarding → `/tenants/new`, the home's New tenant | C5 13:1257 | Five steps (Company, Administrator, First hotel, Modules, Review and finish), the "So far" card and "What Finish sends"; Finish sends `Tenant/Create` then, with a hotel, `Property/Create` with the lines; a refused hotel leaves the tenant and says so; the draft lives in the tab only (no storage) |
| C13 states and dark | the shell's theme switch and tokens (batch 1) | C13 22:3091 | Verified on the rebuilt pages (§6.3) |

Doors added for batch 2 (Identity): the four list facets above, `GetPagedUserAccountsQuery.State/TwoStep/RoleId`,
`IOperatorSessionRepository.LatestStartsAsync` (one read for a page of accounts), `ILocalStaffEnrolmentRepository
.QueryAcrossTenants()`, `UserAccountModel.LastSignInUtc/LastSignInDevice`, `TenantModel` company facts and
`TenantAdministratorModel`, `PropertyModel.TenantCode/ModuleCodes`, the sign-in pages' settings section. The tenant
list maps a sort by `OnboardedOn` (a `DateOnly`, which the framework sorter cannot order) to `CreatedDate`, the same
order. No door was widened beyond ViewTenants / the platform tenant.

### 6.2 Automated checks (batch 2)

| Check | Result |
| --- | --- |
| `corepack pnpm@12.14.0 run check` | 0 problems; 2803 translation units, 37 libraries, 0 boundary violations, 0 cycles |
| `ng test harmony-web` (every library) | 92 files / 336 tests, all green |
| `ng test harmony-console` (the console app, now with the people, roles and settings pages) | 12 files / 29 tests, all green |
| `ng build harmony-console` / `harmony-web` (production, en + ar) | console 479.7 kB initial, hotel app 430.9 kB, no warnings |
| Identity publish `s65/rebuild.ps1 -Target api-s77` | green |
| e2e `s70/tests/e2e/platform-operations-e2e.ps1` against `api-s77` | 15 passed, 0 failed |
| e2e `s71/tests/e2e/console-e2e.ps1` (console walk, now also the reference index, Properties, Groups, the roles matrix, a person page, Invitations, Approvals and rules, Company login, Company directory, the wizard behind Onboarding) | 16 passed, 0 failed (14 / 2 before the second pass, §6.3b) |
| e2e `s56/tests/e2e/angular-page-gate.ps1` (hotel app + console; the console tenant is now born through the wizard) | 44 passed, 0 failed (44 / 2 before the second pass, §6.3b) |

New unit specs: property page, group page, groups list (rewritten), people, person, invitations, rules, company login,
directory, roles matrix, settings, reference index, reference table (rewritten), new-tenant wizard, facet, CSV, and
the tenant page's deep link (`?subscribe=<hotel>` opens the Add module drawer once the view has rendered).

### 6.3 Hands-on, first pass (browser pane, 1366 × 768, 30 Oct)

Walked signed in as the seeded platform person, against `api-s76` first and the fixed builds after: home, Tenants,
the New tenant wizard, the Tenant page and its Add module drawer, Properties, the Property page, Groups, the Reference
index and the Cities table, People, a Person, Invitations, Approvals and rules, Company login, Company directory, the
Roles matrix, Settings; the dark theme on Tenants, Person, the wizard, Cities and Roles; Arabic on Tenants, Person and
the Reference index (mirrored shell, RTL tables, Arabic plurals). Every API call on those pages answered 200 after the
fixes below; the browser console shows no error.

Found on the walk and fixed (each covered by the automated checks above after the fix):

1. **Tenants list answered 500** — `Unsupported sort datatype: System.DateOnly`: the C2 store sorted by `OnboardedOn`
   and the framework sorter has no `DateOnly`. The store sorts by `CreatedDate` (the onboarding day is the creation
   day) and the Identity handler maps an `OnboardedOn` sort to `CreatedDate` for any other client. This also emptied
   the "Choose a tenant" lists of the property and group editors and showed the "Harmony is not responding" bar.
2. **Reference index and table answered 400** for Lookup values: the per-category table was counted without a
   category. Scoped tables now say *per category* instead of a count (`TableCount.total` carries `'scoped'`).
3. **A tenant page opened from a URL that asks for a drawer** (`?subscribe=<hotel>` from the property page's Raise
   the line, `?invite=1` from the list's row menu) **crashed with NG0951**: the drawer is born with the tenant's view and
   the URL effect ran before that view rendered. The page now opens the drawer after the next render and answers each
   URL request once, so a reload after Add does not reopen it. Spec added.
4. **"1 rows"** in every pager → an ICU plural (en and ar, both apps).
5. **Screen-reader-only labels were visible** ("Search" over every search box, "Actions" in table heads, "Second
   administrator" beside the switch): the batch-2 templates used `sr-only`, a class no stylesheet defines; they now use
   the tokens' `.vh` utility (17 templates).
6. **The Approvals and New devices switches** rendered as the filters' bordered pill; they are bare toggles beside the
   words, as C20 draws them.
7. **The Arabic console build failed** on the new *per category* unit → translated.

Observed, not changed: Settings (C11) shows its empty state because no service has registered a key yet (the registry
has 0 rows in this seed; the page fills as services register); Groups is empty in this seed (the gate creates one); the
Person page's roles card shows its editor only after the roles list arrives.

### 6.3b Second pass: every suite screenshot beside its Figma render (30 Oct, afternoon)

The first suite run after the pass above ended 44/2 (gate), 14/2 (console walk), 15/0 (platform operations). The two
failing lines each hid a screen defect, so every screenshot the suites take (`s71/shots/console-*.png`, `s56/shots/
console-*.png`, 1366 × 768) was put beside the frame it implements (C1–C12, C14–C23, rendered from Figma at 1440 ×
900) and read at full size. Found and fixed, then re-run until green:

8. **The page heading held everything in the title row** — code tile, Arabic name and chips were inside the `<h1>`
   (the suites read "BLUENILEBlue Nile Hotelsفنادق…ActiveTier 1" as the tenant's name). `hr-page-head` now keeps the
   heading to the title; the tile or avatar stands beside it and spans the lead too, as C3, C15, C16 and C18 draw it;
   the chips sit beside the heading. Spec added.
9. **"Reference data › Reference data"** above every reference table: a page named after its own section shows no trail.
10. **Disabled solid buttons were grey blocks without words** (Save changes on Settings, Save roles on a person): the
    shared `.btn:disabled` rule set dark lettering on the dark fill at 45 % opacity. Solid buttons keep their light
    lettering when disabled.
11. **Roles matrix**: the system role now leads the columns (C7); "Custom · 1 people" is an ICU plural (en + ar); the
    role menu no longer covers the end of a long role name ("Platform opera···"), and names may wrap.
12. **The person's Roles card**: Save roles moved into the card's head (C18) and the editor drops its own frame inside a
    card; card heads over facts or text lost their rule (C15, C18, C20 draw none; a head over a table keeps it).
13. **Tab counts** read "Devices  · 2" (an 8 px flex gap): one word-space now.
14. **Arabic names under English ones** sat at the far edge of their cell (`dir="rtl"` on the block); they keep the
    cell's start edge inside `<bdi>` (tenants, properties, tenant and group pages).
15. **Groups list**: the lead is the census the frame shows ("No groups yet" / "N groups · by type · N inactive") and
    the empty card says *No groups yet* unless a facet or the search narrows the list (*No groups match*); the same
    for Properties. Spec added.
16. **Tenants list**: the onboarding date and the administrator's note no longer wrap into two lines; the note is cut
    with an ellipsis as C2 draws it.
17. **The settings-registry table showed two "Search settings registry" boxes**: a second text filter now carries its own
    placeholder.
18. **Group page**: the Hotels card's Add hotel is the quiet button of C15, not a solid one.
19. **The suites themselves**: the console walk's person-tab reader wrote `/\s+/` inside a JavaScript template literal,
    which turns `\s` into the letter *s* ("Device  · 2", "Pa key  · 0"); it also still expected the batch-1 heading
    "Currencies" and the role code "SUPERADMIN" where the frames show "Reference data" (with Add currency) and the
    role's name. Fixed in `s71/console-walk.mjs` and `console-e2e.ps1`.

Checked and left as the frames' data differs from the seed: the Settings page's empty state (no registered key), the
company directory and company login "not added" states, the sign-in page's empty e-mail; C8 (Audit log), C9 (Sign-ins)
and C10 (Health) from batch 1 match their frames apart from the §5 decisions. Kept on purpose: the **Find** button
beside each filter row (the shared list toolbar's submit; the frames apply filters on change), the breadcrumb above
list pages (the shell's), and the home's **Refresh** where the frame has **Customize** (§5, decision 2).

### 6.4 Owner decisions added by batch 2 (nothing behind them; left out rather than faked)

13. **Export all** on the reference index, **Import CSV** on a reference table, **Duplicate role** on Roles — no door.
14. **"Used by N hotels"** on a reference row and the cities' *Used by* column — no usage count in the reference reads.
15. **Approval expiry** ("in 6 days") on Approvals and rules — `ApprovalRequest` carries when it was raised, not when it
    lapses; the column is left out.
16. **Sign out everywhere** on a person — Identity signs out one device at a time for another person (`SignOutDevice`);
    `LogoutEverywhere` is for the signed-in person only.
17. **Company login "People using it … kept on a Harmony password"** and the per-domain **Added** date — the company-login
    model carries domains as names only; the console counts people by e-mail domain from the platform census instead.
18. **Directory "last push" and "secret made · by"** — not in `DirectoryConnection`; the card shows what it carries.
19. **Group "Standards"** — shown as the Figma does, marked *Not built*; the settings engine exists, the group route does not.
20. **Regimes in the wizard's hotel step** — the Figma omits them, the API requires them; the step asks for the four.
21. **The console's People/Roles/Settings pages moved into the console app** — the hotel app must not bundle them; a
    later clean-up could give the shared editors a library of their own.
22. **Sign-in pages are English only** (no العربية switch) and **"Continue with Microsoft"** is not a button: the company
    sign-in is chosen by the e-mail's domain (stage A).
23. **Sorting by the onboarding day** — the framework sorter orders `DateTime`, not `DateOnly`; either the framework
    learns `DateOnly` or lists keep sorting by the creation instant (what batch 2 does).
24. **Pager wording** — every list says "N rows · Previous · Page 1 of 1 · Next" (the shared `hr-pager`, also the hotel
    app's); the console frames say "1–7 of 7 ‹ Previous Next ›". Changing it changes the hotel app too.
25. **"Open person" on the tenant's Administrator card** (C3) — the administrator is the hotel tenant's account; the
    console's person pages are the platform's own people, and no cross-tenant person page exists.
26. **"Sign out all" on Your sessions** (C9) — Identity's sign-out-everywhere is the signed-in person's own door; the
    card signs out one device at a time today. Small to add once the owner wants it on the console.
