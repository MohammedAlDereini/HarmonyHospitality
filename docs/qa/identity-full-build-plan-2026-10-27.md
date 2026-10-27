# Plan: finish Identity before the first PMS service (27 October 2026)

Your rule: we do not leave Identity until every part of its design is built. This note is the plan. Nothing here is built yet.

## Where we are

25 of the 58 tables in `identity-erd.html` are built. Items 2-4 are built but not committed.

| Part of the design | Built | What it is |
|---|---|---|
| Reference data, organisation, settings, accounts and access | 24/24 | Done |
| Signing in | 0/6 | Company login (Microsoft, Google), invitations, sessions, "confirm it is you" |
| Revocation | 0/5 | One feed that tells every service "this person's access is gone" |
| Approvals and audit | 1/8 | Two-person approval for dangerous actions (the audit log is built) |
| Directory sync | 0/12 | Pull a customer's staff from their Microsoft or Google directory |
| Plumbing | 0/3 | Outbox, inbox and receipts as their own tables |

Some of this already works in a simpler form. It is not yet in the tables the design asks for:

- **Invitations:** sent by e-mail today.
- **"Confirm it is you":** a 5-minute mark in Redis today.
- **Sessions:** Duende's own session table plus our device list.
- **Taking access away:** a security version in Redis that every service checks today.
- **Plumbing:** the framework's outbox, Redis locks and the receipt gate.

## What the licence allows (checked in the Duende 8.0.10 package)

The licence is Duende **Community Edition**.

| Duende feature | In our licence |
|---|---|
| Server-side sessions, PAR, DPoP, CIBA, resource isolation | yes |
| Dynamic identity providers (a different login provider per customer) | **no** |
| SAML 2.0, in either direction | **no** |
| Automatic key management | **no** (we already manage keys ourselves) |
| Limits | 10 clients, 1 issuer |

Without dynamic providers, the simplest way to give customers company login is this:

- One Harmony app is registered with Microsoft and one with Google.
- Each customer's row stores *their* Microsoft or Google tenant.
- At sign-in we check that the person's company login belongs to that customer.

This matches the columns the ERD already has: Scheme, Issuer and Audience on FederatedAuthorities.

## The stages

Each stage is built in small steps with one quick test each. The full regression runs once at the end.

**Stage A: company login** (the most visible part)
1. UserAccounts gets the ERD's missing columns: Issuer, Subject, IsServicePrincipal, IsMfaEnrolled, SupersededByUserAccountId.
2. FederatedAuthorities: each customer has a "local" row, plus Microsoft or Google rows if they use company login. These are set up from the console.
3. ExternalIdentityLinks and their periods link a person to their company account. Sign-in finds the person by issuer and subject.
4. The sign-in page shows "Sign in with Microsoft" and "Sign in with Google". For the local proof, a second copy of Harmony Identity plays the customer's company login, so no outside account is needed.

**Stage B: the rest of signing in**
1. LocalStaffEnrolments: invitations move into the table, holding only a digest of the handle, never the plain value.
2. OperatorSessions: who is signed in, and whether they passed two-step.
3. StepUpAuthorizations: "confirm it is you" moves into the table. Its expiry is set once and never extended.

**Stage C: the revocation feed**
1. The sequence and the feed. Sign-out everywhere, suspension and role changes each add a row.
2. Snapshots and gap tombstones, plus a cleanup job that always leaves a tombstone.

**Stage D: approvals**
1. SecurityOperations.
2. Maker-checker, five tables in one aggregate. The person who approves is never the person who asked. A request is void if its target changed after it was raised.
3. MergeApprovals (the merged account is marked as replaced, never deleted) and QuarantineReleases.

**Stage E: directory sync**
1. Connection, allowed groups, lease and fencing token. A worker that wakes up late is refused.
2. Runs, streams, pages and delta tokens, read from Microsoft Graph with plain HTTP calls and no SDK.
3. MigrationAmbiguities: a directory user who matches two accounts waits for a person and is never guessed.

**Stage F: plumbing**
1. OutboxMessages written in the same save as the data. This is decision 1 in the items 2-4 review.
2. InboxMessages and RequestIdempotency tables.

**Then:** the full regression and the browser walk, then STOP before the first PMS service.

## Stage A in detail: company login

### How it works

This is Duende's own standard way of doing external login. Duende has no Microsoft or Google handler of its own. Its documentation says to register ASP.NET Core's handler (`AddOpenIdConnect`) and to write the callback page yourself, where you "find your user in your database, or provision a new user". The only paid Duende piece is dynamic providers, which we don't need with one shared Microsoft app and one Google app. One handler package covers both providers.

**Who does what (revised 27 Oct after the user's review, the Mews way):**

```
 Platform (us)     allows company login for the customer (Mews: "Request" → approved)
 Customer admin    picks Microsoft or Google, enters the company's e-mail domains,
                   sends the consent link to their IT, switches it on
                   → everyone whose e-mail is on those domains signs in with the company (default)
                   → the admin can move one person back to normal login (password + two-step)
                   → stage E: directory sync brings staff in automatically
 Staff             type their e-mail, go to Microsoft, come back signed in. Nothing else.
```

**Sign-in, e-mail first (like Mews):**

```
 e-mail typed ─► domain belongs to a customer with company login, person not an exception?
                   yes → Microsoft (domain_hint)        no → password page as today
 Microsoft returns: iss, tid, oid, email
   1. iss is exactly this customer's registered company login?      no → refused
   2. (company, oid) already linked?  yes → that account
                                      no  → first time: the account with this e-mail, inside
                                            this customer, e-mail domain on the customer's list
                                            and verified by the company → link it now (oid kept)
   3. account active, customer active, person not an exception?     no → refused
   4. signed in (session marked "ext"), trailed
```

The e-mail is used once, to find the account on the first company sign-in, and only inside a company that is already proven. Every later sign-in uses Microsoft's permanent id. This respects Microsoft's warning against deciding access by e-mail.

**How the others do it (checked 27 Oct):**
- **Mews:** the customer's admin sets SSO up in the Mews settings (Security, then Single sign-on). A single property requests it first and Mews approves. The admin gives the company's e-mail domains and identity provider: Azure/Entra, Google Workspace, Okta, OIDC or SAML. All users on those domains must use that provider. Someone removed from the company's provider "lose[s] access to Mews automatically". SCIM user sync is a separate paid add-on.
- **OPERA Cloud:** each customer gets its own Oracle identity domain. The customer federates its own SAML 2.0 provider (Entra or Okta) into it, optionally with just-in-time user creation. Oracle says this "eliminates the necessity to separately manage users".

Facts this rests on, from Microsoft's documentation:
- A multi-tenant app signs people in through the `organizations` endpoint. It must check that `iss` is exactly `https://login.microsoftonline.com/{tid}/v2.0`, using the token's own `tid`, and it must also check the signing key's issuer.
- A person is identified by `tid` plus `oid`. `oid` is also the id Microsoft Graph returns, so stage E's directory sync matches the same people.
- Microsoft says never decide access by `email`, `preferred_username` or `upn`. Linking by matching e-mail is therefore ruled out.

Today's facts in Identity:
- One e-mail is one account on the whole platform, and the sign-in page has no customer field.
- Invitations go out by e-mail, and the person sets a password.
- All sign-ins end in one place: `SignInSession.SignInAsync` with the method names.

### Steps (each with one quick test)

1. **Tables.**
   - UserAccounts gets Issuer, Subject, IsServicePrincipal, IsMfaEnrolled and SupersededByUserAccountId.
   - The FederatedAuthorities table is added, and every customer gets its "local" row (backfilled, and created at birth).
   - ExternalIdentityLinks and ExternalIdentityLinkPeriods are added.
2. **Platform allows it** in the console for a customer, like Mews approving a request. A customer that is not allowed sees nothing.
3. **Customer admin sets it up** in the web app:
   - Pick Microsoft or Google, and enter the company's e-mail domains. Each domain belongs to one customer only.
   - For Microsoft, Identity reads Microsoft's public metadata for the domain and stores the exact issuer, which carries the tenant id.
   - Send the consent link to the company's IT, then switch company login on.
   - Every change is trailed.
4. **Sign-in page, e-mail first.**
   - An e-mail on a company-login domain goes to Microsoft or Google.
   - Microsoft's OpenID Connect handler is registered once and static, as Duende Community Edition allows.
   - The callback page runs the checks above, links on the first company sign-in, signs the person in through `SignInSession` with method "ext", and trails the result.
   - The local proof uses a fake company login: a second copy of Harmony Identity with its own issuer.
5. **Admin manages each person's login** on the person page:
   - Company login is the default for the company's domains.
   - The admin can move a person to normal login as an exception (a password set through the usual invitation mail).
   - The admin can see the person's company link and end it, which closes the period.
   - One fallback administrator with a password is required while company login is on.
6. **Google:** the same flow. Google's `iss` is always `accounts.google.com`, so the customer's Workspace domain (the `hd` claim) needs its own column on FederatedAuthorities.
7. **Proof:** suite s61 (company login) and a browser walk, then the full regression at the end of the whole plan.

### Built (27 October, uncommitted)

- **Framework:** codes 00118-00128 (CompanyLogin) and 10901-10906.
- **Identity:**
  - FederatedAuthorities with FederatedAuthorityDomains, ExternalIdentityLinks with their periods, and the six user columns. Migration `CompanyLogin` backfills each customer's local row and each account's subject. A customer is born with its local row, and so is the platform on a fresh install.
  - The doors live in `api/CompanyLogin`: Add (platform), of/{tenant}, mine, AddDomain, RemoveDomain, Switch, account/{id}, PasswordSignIn and EndLink.
  - The OpenID Connect handlers for Microsoft and Google run Microsoft's issuer and signing-key checks. The callback page is CompanySignIn. The login page routes by e-mail before any password is checked, and passkeys are refused for company people.
- **Web app:** a Company login page (Users, then Company login) and a "How they sign in" section on the person page. **Console:** a company login panel on the customer page. Wording and Arabic are included for all of it.
- **Proof:**
  - Suite s61 passes 10 of 10 against a local stand-in for Microsoft and Google: the schema, the platform's guards, the admin's guards, the fallback rule, the first sign-in link, the repeat sign-in, and every refusal. The refusals include a forged key and a foreign issuer. The suite also covers Google's hd, switching off, and a suspended customer.
  - The migration upgrade and its rollback were tested on a copy of the development database.
  - Frontend: check 0, 217 web tests and 2 console tests pass, and both apps build. The Arabic web build is 401.0 kB, a warning only.
- **Browser walk** on the local stack (TestEnv\s61\hands-stack.ps1, which writes LOCAL-RUN-company-login.md):
  - Lina typed only her e-mail, went through the Microsoft stand-in, and landed in the web app. The trail shows `account.company-linked`, then `sign-in.company`, and her link holds the stand-in's oid.
  - Her person page shows "How they sign in" with the link, in English and in Arabic (right to left).
  - The Company login page shows Microsoft, on, for ritz.test, with the consent link.
  - The console customer page lists the company login and words the refusal of a second Microsoft (00120).
  - **A bug found only in the browser:** the sign-in page's `form-action` allowed only Harmony's own origins, so Chrome blocked the redirect to Microsoft. It now also lists the configured providers' sign-in hosts. s61 checks this, so the suite has 11 checks now.
- **Full regression CL1** on api-s61: all 34 suites, 911 checks, 0 failures. It includes s61 and the 44-step browser gate (TestEnv\s35\regression-CL1\summary.txt).
- **Not proven by a suite:** the passkey refusal for company people, and a real Microsoft or Google tenant, which needs the owner's app registrations.
- **Packages:** only `Microsoft.AspNetCore.Authentication.OpenIdConnect` 10.0.13 was added, as an explicit reference. Duende already brought it in. Microsoft's two issuer checks are written by hand, following their documented steps, instead of adding the `Microsoft.IdentityModel.Validators` package.

### Known gap until stage E

When someone leaves the company, Microsoft blocks their next sign-in. A Harmony session that is already open lives until it ends: 30 idle minutes at Identity, or the BFF's session at most 12 hours. Directory sync in stage E closes this by suspending leavers.

## Decisions for you

1. **Company login approach.** I recommend one Microsoft app and one Google app shared by all customers, with each customer's tenant stored on their row. "Any provider per customer", Okta for example, would mean building our own loader or buying Duende's add-on.
2. **Package.** Company login needs `Microsoft.AspNetCore.Authentication.OpenIdConnect` 10.0.x, which is Microsoft's own sign-in handler. Yes?
3. **SAML.** It is not in the licence. Skip it? Microsoft, Google and Okta all speak OpenID Connect.
4. **Directory sync source.** Microsoft Graph first, Google later. Graph is an outside API. Yes?
5. **Revocation.** I recommend that Identity writes the feed while services keep the fast security-version check they have now. The other choice is that every service also reads the feed, which is framework work.
6. **Which actions need a second person.** My suggestion:
   - giving or raising an administrator role
   - suspending or ending a customer
   - ending a two-step exemption
   - merging accounts
   - releasing quarantine
   - turning company login off for a customer
7. **Plumbing.** Build the stricter outbox? This is the same question as decision 1 in the items 2-4 review.
8. **Going live** needs accounts that only you can create: a Microsoft Entra app registration and a Google OAuth client. Each customer's admin also has to approve access to their directory. None of this is needed for the local proof.
9. **Order.** I recommend committing items 2-4 first, so this work starts on a clean base.
