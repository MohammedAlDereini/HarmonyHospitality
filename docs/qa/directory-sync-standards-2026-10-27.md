# Staff from the company directory: the standards, what the best do, and the design (27 October 2026)

Stage E brings a hotel company's staff into Harmony from its own directory (Microsoft Entra ID, formerly Azure Active
Directory; Okta; Google Workspace). This note collects the standards and the vendors' own documents, then proposes the
design. The design docs (identity-erd.html cluster eight) planned a **pull** from Microsoft Graph; every standard and
both reference vendors use a **push** (SCIM). The owner chose push; it was built on 27 October (stage E, suite s65 25/25)
and cluster eight is redrawn to three tables.

## 1. The standards, in one picture

```
SIGN-IN            OpenID Connect / SAML 2.0            done (stage A, OIDC)
SESSIONS           IPSIE SL1-SL3 (OpenID Foundation)    mostly done (see 4)
STAFF IN AND OUT   SCIM 2.0: RFC 7642/7643/7644,        stage E
                   RFC 9865 (cursor paging, Oct 2025),
                   IPSIE AL1-AL3 SCIM profile (draft)
REAL-TIME KILL     OpenID Shared Signals 1.0 + CAEP 1.0  later
                   + RISC 1.0 (Final, Sep 2025)
RULES WE MUST MEET PCI DSS 4.0 8.2.5 / 8.2.6, CIS v8 5.3, NIST SP 800-63C-4 (RP subscriber accounts)
```

**IPSIE** (OpenID Foundation working group: Interoperability Profiling for Secure Identity in the Enterprise) is the
industry's own answer to "what exactly must an enterprise SaaS do". Its levels:

| Level | The application must |
|---|---|
| SL1 | single sign-on at NIST FAL2; take the session lifetime from the assertion; code flow with PKCE |
| SL2 | on request, re-authenticate the user, and revoke active sessions and tokens; no unsolicited assertions; termination within 15 minutes, or 1 hour if tokens are device-bound |
| SL3 | exchange session, user and device changes with the identity service continuously; within 5 minutes (15 if device-bound) |
| AL1 | suspend, archive or delete users when the identity service says so |
| AL2 | accept users and groups pushed **before** first sign-in; **no local create, update or delete** of those users or their profile; map groups to roles |
| AL3 | expose its roles to the identity service, which assigns them |

The **IPSIE SCIM profile** (AL draft, openid/ipsie-scim-al) turns AL1-AL2 into SCIM rules for the application:

- **Authentication:** OAuth 2.0 client_credentials with JWT client authentication (RFC 7523), authorization server metadata (RFC 8414), a `scim` scope, short-lived tokens, replay protection.
- **Deactivation:** PATCH active false/true. Within **5 minutes**, revoke "all access mechanisms": web sessions, API tokens and refresh tokens. DELETE is optional; if offered, the same userName must be creatable again.
- **Required filters:** `userName eq`, `externalId eq`, `emails[value eq]`, `emails[type eq "work" and value eq]`. For groups: `displayName eq`, `externalId eq`, `members[value eq]`.
- **Groups:** creatable empty, PATCH of at least 50 member changes in one request, displayName unique.
- **Limits and errors:** at least 25 requests per second per tenant; 429 with Retry-After; at most 1,000 per page (cursor paging recommended); SCIM error format with no internal details.
- **Credentials and audit:** never a password attribute; every change logged.

## 2. What the identity providers actually do

**Microsoft Entra ID** (the provisioning service; Microsoft's documents on SCIM endpoints and how provisioning works):
- **Credentials:** each customer gives its own client id and secret (OAuth client credentials), or uses Workload Identity Federation (Entra signs a short JWT, RFC 7523; no stored secret). A single app-wide credential is not supported; long-lived bearer tokens are not allowed for new gallery apps.
- **Schedule:** a first full cycle, then every **40 minutes**. Failed objects are retried with back-off. Mostly failing calls send the job into quarantine (once a day), and after 4 weeks it is disabled. Accidental-deletion protection is available.
- **Leavers:** unassigned, out of scope or blocked → PATCH `active: false`. Hard-deleted (30 days after soft delete) → DELETE.
- **Behaviour our endpoint must accept:**
  - `op` arrives as `Add`, `Replace`, `Remove` (case-insensitive).
  - Filters use only `eq` and `and`.
  - Answers are `application/scim+json`, and values are stored exactly as sent.
  - "Test connection" reads a random user.
  - Groups need PATCH and a unique displayName.
  - TLS 1.2 with the listed cipher suites.

**Okta:**
- Deactivation is a PATCH **without a path**: `{"op":"replace","value":{"active":false}}`.
- Some Okta integrations update users with PUT. Our endpoint must take PUT and PATCH, with and without a path.
- Okta deactivates; it does not delete.

**Google Workspace:**
- Google pushes users only to apps in its own catalog. A custom SAML or OIDC app gets no generic SCIM push. This comes from vendor reports; re-check against Google's current help before relying on it.
- For a Google customer the choices are sign-in plus first-sign-in linking (what stage A does), or Harmony reading Google's Directory API (domain-wide delegation, heavy).

## 3. What the two reference PMS vendors do

**Oracle OPERA Cloud** (Identity Federation Overview 26.1, March 2026):
- **Sign-in:** SAML 2.0 only, through an Oracle identity domain per customer. Works with any SAML 2.0 identity provider.
- **Staff in,** three ways:
  1. JIT: the user is created at first SAML sign-in.
  2. "Microsoft Azure AD Synchronization": the customer adds Oracle's app from Entra's gallery and turns on automatic provisioning. **Entra pushes** users, groups and memberships (SCIM) into the customer's Oracle identity domain.
  3. A **SCIM endpoint** for any other SCIM identity provider.
- **Roles come from group names:** `<PROPERTY>-<ROLE>`, `<CHAIN>-<ROLE>`, `<ENTERPRISE>-<ROLE>`, in upper case.
  - Oracle's seeded groups (`HOTEL1-ADMIN`, `HOTEL1-OPERACASHIER`, ...) are created in the customer's own directory.
  - Custom groups are mapped to permissions in OPERA's Role Manager.
  - A group that breaks the naming rule is ignored.
  - Administrator groups do come from the directory.

**Mews** (help centre and developer blog):
- **Sign-in:** a company login with Entra, Google Workspace or Okta, free since March 2026.
  - Needs a company e-mail domain; one identity provider per domain.
  - Device authorisation is skipped when sign-in is enforced through the company.
  - People removed at the company lose access automatically.
- **Provisioning:** SCIM 2.0, part of its "Identity Access Management" product. The hotel's provider calls Mews with a URL and a token from Mews' Security page.
- **Not public:** Mews' sign-in protocol and its group-to-role mapping.

## 3b. Google Workspace in depth (researched 27 Oct)

**What Google offers an app like Harmony:**

| Tool | What it gives | Limit |
|---|---|---|
| OIDC "Sign in with Google" (Harmony's stage A) | who the person is (`sub`, `hd`, `email_verified`) | **no groups in the token** |
| Custom SAML app + "Group membership" mapping | the person's groups inside each SAML sign-in | at most 75 groups; only the groups the admin lists |
| Automatic provisioning (SCIM push) | joiners and leavers pushed | **only apps in Google's own catalog**, never custom apps |
| Admin SDK Directory API (read, admin consent) | users and groups on request | the hotel's Google admin must consent; deleted users are visible only about 20 days |
| Directory API push notifications (`users.watch`) | a call when a user is added, deleted, made admin, restored or updated | a webhook channel that expires and must be renewed |
| Cross-Account Protection (RISC) | security events for Google accounts | Google says it "does not currently send security events for Google Workspace" users |

**OPERA Cloud with Google:**
- **No Google-specific guide.** Oracle publishes guides for Entra, Okta and "Generic"; a Google customer uses the generic SAML guide with "create at first sign-in" (G47676-02, Dec 2025).
- **The Google SAML app must send** `oc_userid`, `oc_surname`, `oc_emailaddress`, `oc_primaryworklocation` (`HOTEL:P`, `CHAIN:C`, `ENTERPRISE:E`) and **`oc_role`**, the group memberships, which are mandatory.
- **Roles come from the groups in each sign-in.** Group names follow `HOTEL-ROLE`.
- **Users are created or updated at each sign-in.** Nothing is pushed, so a leaver simply cannot sign in any more.
- **OPERA's generic SCIM door can't help a Google customer,** because Google pushes only to catalog apps.

**Mews with Google:**
- **Sign-in** runs through Auth0's Google Workspace connection. The hotel creates its own Google OAuth client and enters the domain, client id, secret and secret expiry in Mews (SSO setup article).
- **Mews' SCIM guide covers Entra only.** Nothing public describes Google provisioning, so for a Google hotel, staff are created in Mews by its admin and then sign in with Google.
- **Leavers:** removed at Google, they can no longer pass the Google sign-in. Their Mews password is the fallback, used only when company sign-in is off or broken.
- **What Mews could use:** its sign-in provider (Auth0) sells a "Google Workspace Directory Sync" that **reads** Google every 30 minutes, users and groups, with the Google admin's consent. Nothing public says Mews uses it.

**Options for Harmony and Google:**
- **G1. Sign-in only, the Mews way (now).**
  - Google sign-in (built) plus first-sign-in linking (built).
  - The hotel admin creates staff and gives roles in Harmony.
  - Leavers are blocked at their next Google sign-in, plus the inactivity rule.
- **G2. The OPERA way: Google SAML with group membership.**
  - Roles come from the groups at every sign-in.
  - It needs SAML in Harmony. Duende's SAML is not in our licence, and ASP.NET has no built-in SAML, so it means a new library (the owner's yes).
- **G3. The Auth0 way: read Google's directory with the hotel admin's consent.**
  - Users and groups every 30 minutes, plus `users.watch` notifications for fast leavers.
  - It fills the same group, membership and role tables as SCIM, keeps OIDC, and needs no new package.

## 4. Where Harmony stands against this today

| Rule | Harmony |
|---|---|
| SL1 sign-in | done: code + PKCE + PAR, FAL2-style signed assertions, amr and auth_time read (login review fixes) |
| SL2 terminate on request within 15 minutes | done: a suspension bumps the security version (checked at every call through Redis); refresh refused; back-channel logout to the BFF |
| AL1 revoke within 5 minutes | done for anything that bumps the version (suspend, terminate): immediate |
| PCI DSS 8.2.5 terminated users revoked immediately | done for Harmony actions; for company leavers it depends on stage E |
| PCI DSS 8.2.6 inactive accounts disabled within 90 days | **not built** (needs a nightly job; local and Google accounts above all) |
| CIS v8 5.3 dormant accounts disabled after 45 days | **not built** (same job, a stricter number) |
| AL2 no local edits of directory-managed people | **not built**: a Harmony admin can still rename them or create accounts on the company domain |
| Shared Signals / CAEP receiver | not built (later stage) |

## 5. Proposed design for stage E (push, SCIM 2.0)

```
Hotel's Entra / Okta ──SCIM 2.0──► Harmony /scim/v2/{Users,Groups}   (one door, every provider)
                      ◄─token──── Harmony /scim/token (client credentials, per customer; Entra WIF later)
Google customers ──── sign-in + first-sign-in link (stage A) + inactivity rule (no Google pull now)
```

- **One connection per customer.** It holds its own SCIM client id and a hashed secret (Entra's rule: no app-wide credential).
- **The token endpoint is our own small door, not Duende.** Duende's licence counts every OAuth client as a "Connected Application", and the Community key allows 10. Per-customer SCIM clients in Duende would use them up.
- **Users:**
  - **Requests to accept:** POST, GET (by id and by the four filters), PATCH (with and without a path, `op` in any case), PUT.
  - **`externalId`** is the directory's id; the existing ExternalIdentityLinks carry it, so the sign-in link and the provisioning link are the same row.
  - **`active: false`** suspends the person. The version bump revokes everything at once, well inside 5 minutes.
  - **`active: true`** reinstates.
  - **DELETE** terminates the account and releases its userName, or is refused; this is a decision.
- **Groups:**
  - SCIM Groups are stored as the customer's directory groups, with their members.
  - The customer's admin maps a group to a Harmony role in Harmony. This is IPSIE AL2.
  - OPERA's naming convention (`HOTEL-ROLE`) is an optional shortcut.
- **Directory-managed people are read-only in Harmony** (IPSIE AL2): name, e-mail and state change only through the directory.
- **Logs:**
  - Every SCIM request is logged: who, what, the result. This is the "run log", without runs.
  - The provider's answer codes follow the SCIM error format, and a 429 always carries Retry-After.
- **Safety Harmony keeps** (beyond the standards, hotel-specific):
  - An e-mail that already belongs to another customer is held as a MigrationAmbiguity, never guessed.
  - An administrator-level role arriving from a group goes through stage D approval when the customer requires approvals.
- **Tables:** compared with the ERD's twelve, the pull-only ones go: leases, fencing, delta bookmarks, run sequence, run pages, run streams. Connections, groups, memberships, a request log and ambiguities stay. The ERD's cluster eight changes; the owner decides.
- **Later:**
  - Entra Workload Identity Federation (no stored secret).
  - RFC 9865 cursor paging.
  - A Shared Signals (CAEP/RISC) receiver for instant "session revoked" from Okta.
  - Exposing Harmony's roles to the directory (AL3).

## 6. Decisions for the owner

1. **Push (SCIM, the standard; OPERA and Mews) instead of the planned pull (Graph)?** Recommended: push.
2. **New people from the directory: DECIDED 27 Oct, the Mews way.**
   - A pushed person waits with no access until they are in a group the customer's admin has set up (hotels, role, done once per group).
   - Then a sign-up e-mail goes out; the person confirms it and sets a Harmony password. As in Mews, the password is the fallback when company sign-in is off or broken.
   - A group set to an administrator role gives that role at once; no second-admin step.
   - There is no per-person release and no Quarantined state, so the ERD's QuarantineReleases are not needed.
3. **DELETE: DECIDED 27 Oct, option A (RFC 7644 §3.6).**
   - **`active: false`** is the real leaving moment: the person is suspended and sessions are killed at once.
   - **DELETE** terminates the account, which then answers 404 and drops out of SCIM searches. The e-mail is freed for a new account; the row and its audit history are kept, never hard-deleted.
   - **Rehire:** a person turned back on keeps the same account, with roles rebuilt from today's groups.
   - **Later:** a retention rule wipes the name and e-mail after the agreed period.
4. **Google customers: DECIDED 27 Oct, G1 now and G3 later.**
   - **Now (the Mews way):** Google sign-in plus first-sign-in linking, which is built. The hotel admin creates staff in Harmony, and the inactivity rule applies.
   - **Later, when a Google hotel asks:** read Google's directory with the hotel admin's consent, users and groups every 30 minutes, plus `users.watch` notifications for leavers. It fills the same group, membership and role tables as SCIM.
   - **Not chosen:** G2, the OPERA way with SAML groups, because it needs a SAML library.
5. **The inactivity rule: DECIDED 27 Oct, option C.**
   - A nightly job suspends any account not signed in for N days, records "inactive N days" and kills its sessions. History is kept, and an admin can turn the account back on.
   - N is 90 by default (PCI DSS 8.2.6). Each customer's admin may set it stricter (for example CIS 5.3's 45), never longer than 90.
   - Not touched: people managed by an Entra or Okta directory (their directory decides), and the platform's break-glass administrator.

**All five decided on 27 Oct.** The ERD's cluster eight (design docs) is to be redrawn to this SCIM design before E1.

## Sources

- IETF: [RFC 7643 SCIM schema](https://www.rfc-editor.org/rfc/rfc7643), [RFC 7644 SCIM protocol](https://www.rfc-editor.org/rfc/rfc7644), [RFC 9865 SCIM cursor pagination](https://www.rfc-editor.org/rfc/rfc9865)
- OpenID Foundation: [IPSIE charter](https://openid.net/wg/ipsie/charter/), [IPSIE levels](https://github.com/openid/ipsie/blob/main/ipsie-levels.md), [IPSIE SCIM AL profile draft](https://github.com/openid/ipsie-scim-al/blob/main/draft-openid-ipsie-al-scim-profile.md), [Shared Signals, CAEP and RISC final (Sep 2025)](https://www.helpnetsecurity.com/2025/09/22/openid-standards-real-time-security-event-sharing/)
- Microsoft: [Build a SCIM endpoint for Entra provisioning](https://learn.microsoft.com/entra/identity/app-provisioning/use-scim-to-provision-users-and-groups), [How provisioning works](https://learn.microsoft.com/entra/identity/app-provisioning/how-provisioning-works), [What is app provisioning](https://learn.microsoft.com/entra/identity/app-provisioning/user-provisioning)
- Okta, as reported by integrators: [Okta and Entra SCIM requests compared](https://workos.com/blog/okta-entra-id-scim-requests), [Okta support: update methods](https://support.okta.com/help/s/article/differentiate-scim-update-methods-for-okta-app-integration-wizard-and-okta-integration-network-applications)
- Google, vendor reports, to re-check: [custom apps without generic SCIM](https://openworklabs.com/docs/cloud/scim-google-workspace.md)
- Oracle: [OPERA Cloud Identity Federation Overview 26.1](https://docs.oracle.com/en/industries/hospitality/identity-management/ocimo/G50272_01.pdf), [OPERA identity bootcamp: Entra federation and sync](https://github.com/rkeerti/opera-cloud-identity-management-bootcamp/blob/main/federatedazure.md)
- Mews: [SSO FAQ](https://help.mews.com/s/article/Single-Sign-on-FAQ?language=en_US), [Securing Mews Operations accounts](https://help.mews.com/s/article/Securing-your-Mews-Operations-user-accounts?language=en_US), [Developer blog: SCIM 2.0 service](https://developers.mews.com/embarking-on-a-microservice-journey/)
- Rules: [PCI DSS 4.0 8.2.6](https://myctrl.tools/frameworks/pci-dss-v4/8-2-6) (8.2.5 to be read in the PCI SSC text), NIST [SP 800-63C-4](https://www.nist.gov/publications/nist-sp-800-63c-4digital-identity-guidelines-federation-and-assertions)
