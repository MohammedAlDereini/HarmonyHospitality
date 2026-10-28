# Identity stages B to F: decisions and build ledger (27 October 2026)

The user said "do all this in one go" for stages B to F. These decisions are taken under that instruction, using the recommendations already given. Every stage is built, tested by its own suite, and then the whole regression runs once at the end. Nothing is committed until the user says so.

## Decisions taken

- **B, invitations:** each invitation becomes a LocalStaffEnrolments row.
  - It records the inviter, the local authority, a SHA-256 digest of the e-mail, a state (Pending, Redeemed, Cancelled) and ExpiresAtUtc 24 hours after sending.
  - UserAccountId is added to the design's columns, so the admin can resend or cancel.
  - Resending or cancelling changes the account's security stamp, so the old link dies. An expired or cancelled invitation can never set a first password.
- **B, sessions:** each sign-in writes an OperatorSessions row: started, ended, two-step done, and Duende's session id.
  - It ends on sign-out, on signing out one device, on "sign out everywhere" and on any security-version bump.
  - A job closes rows whose Duende session has gone, after an idle timeout or expiry.
  - Rows are kept as long as the security trail (12 months), then the retention job removes them.
  - A company sign-in counts as two-step done only when the company says so (Microsoft's amr names mfa); Google never says it. Changed on 27 Oct by the login review (docs/security/login-deep-review-2026-10-27.md, M3).
- **B, confirm it is you:** StepUpAuthorizations replaces the 5-minute Redis mark.
  - The expiry is set once and never extended.
  - Company people confirm by going back to Microsoft or Google with a forced sign-in (prompt=login, max_age=0).
- **C, revocation feed:** Identity writes the feed in the same transaction as the security-version bump.
  - It keeps a sequence per customer, plus snapshots and gap tombstones.
  - A job compacts entries older than 7 days into a snapshot and always leaves a tombstone.
  - Read doors are for the platform only. Services keep their fast Redis security-version check; a framework reader is a later ticket.
  - SecurityOperations names each change, and feed entries point at it.
- **D, approvals:** a customer switch, Tenant.ApprovalsRequired, set by its administrator. It is off by default, so today's behaviour does not change.
  - When it is on, these actions need a second person: giving an administrator-level role (by hand, or by giving a directory group such a role), exempting from two-step, switching company login off, and merging accounts.
  - The person asking must hold a live StepUpAuthorization. The checker is never the maker. A request is void if its target's security version moved.
- **E, directory sync (changed 27 Oct, see docs/qa/directory-sync-standards-2026-10-27.md):** SCIM 2.0 push from Microsoft Entra ID and Okta, one door, the way Mews and OPERA do it. Google: sign-in only now (G1), reading Google's directory later (G3).
  - Three tables (ProvisioningConnections, DirectoryGroups, DirectoryGroupMembers) and three columns (UserAccounts.ProvisioningConnectionId and DirectoryExternalId, Tenants.InactivityDays). The pull-only tables, the quarantine and MigrationAmbiguities are gone.
  - The directory proves itself with the connection's client id and secret: a one-hour token from Harmony's own token door (client credentials; not Duende, whose licence counts clients), or the long "clientId.secret" token (Okta, Mews). A new secret kills both at once.
  - The group is the gate (the Mews way): a pushed person has no access until a group the customer's administrator gave a role takes them in; then the sign-up mail goes out. Their roles follow their groups only.
  - active=false suspends at once; active=true brings them back with today's groups; DELETE terminates and frees the e-mail (RFC 7644 3.6). An e-mail of another customer is a 409 back to the directory.
  - Two Mews rules: the e-mail never changes through SCIM; directory people are read-only in Harmony until the platform removes the connection.
  - The inactivity rule (PCI DSS 8.2.6): a nightly job suspends accounts with no sign-in for the customer's number of days (90 unless fewer); directory people and the super role are left out.
- **F, plumbing (changed 27 Oct after checking the framework and Microsoft's guidance: "the smaller F" plus five fixes):**
  - The outbox is the framework's own table (Outbox.OutboxContainerEntries). Identity writes each event into it in the same save as the data (IdentityOutbox, in IdentityDbContext.SaveChangesAsync); the after-commit publishers are gone.
  - Framework fixes: RabbitMQ publisher confirms; the job sends in row order, one sender across servers (sp_getapplock), stops at the first failure and retries with a growing wait (10 s doubling, at most 5 minutes); after 10 tries a row is set aside, logged as an error and reported by an "Outbox" health check; sent rows stay Completed 7 days; the job reads every tenant (the context's tenant filter no longer applies to it); the table is created at startup.
  - The inbox is the framework's request table (ClientRequest) mapped as InboxMessages, written in the receiver's own save, keyed by a hash of receiver + message id, cleaned after 14 days. Identity consumes nothing yet: a test receiver proves it.
  - Request keys stay on the receipt gate's Redis store; later.md L1b item 6: Redis saved to disk (AOF) and noeviction.

## Notes from the build

- C: the feed line is written by the Identity database context itself, so no handler can forget it. New accounts start at security version 2 (their roles are set at creation); a new account has no tokens, so it writes no line.
- C: the compaction door takes KeepDays (0 to 30) so tests and the platform can compact now; the hourly job keeps 7 days.
- D: Rejected is added to the ERD's states (pending, approved, void). Requests also keep the change itself (Payload) and the account it is about (TargetUserAccountId), which the ERD did not list.
- D: an approval runs the stored change through its own handler, inside one transaction with the execution record. Every rule of that handler is checked again against the checker. A refusal rolls everything back and the request stays waiting.
- E: the request's database context reads the customer once, when it is made, so the SCIM door checks the directory's token in a scope of its own and sets the customer before anything else touches the database.
- E: directory sign-up invitations name the person as their own sponsor (LocalStaffEnrolments needs one; the directory sent them).
- E: not covered by s65: the nightly timer itself (the platform door runs the same round), a role across a hotel group (the same assignment code as one hotel), and Entra's Workload Identity Federation (later).
- F: the framework's health page builds its list of checks itself on every call, so the outbox check joins it through the framework's HealthCheckOptions (a plain AddHealthChecks().AddCheck is cleared).
- F: the framework turns a unique-key violation into a BusinessException "Conflict"; SCIM answers 409 for it (it answered 400 before).
- D: the gated changes need ManageUsers, the same permission as the approve door. Switching company login off is gated in code; the suite does not drive it (it needs a company login set up), s61 still covers switching it off with approvals off.

## Ledger

- [x] B1 invitations (s62)
- [x] B2 sessions (s62)
- [x] B3 confirm it is you (s62, s61 company confirm)
- [x] C revocation feed (s63 5/5: one line per bump in the same transaction, six parallel writes without holes, platform-only doors, compaction with snapshot and tombstone)
- [x] D approvals (s64 7/7: off by default, maker confirms first, checker never the maker, void when the account moved, merge rejected then approved, switching off approved too)
- [x] E directory sync (s65 25/25: the token door both ways, Entra's test connection, users created, taken over, filtered, renamed, e-mail change refused, groups as the gate with the sign-up mail, Okta's member removal, read-only, off and on, delete and the e-mail freed, a new secret, switched off, the platform's removal, the inactivity rule, a role at one hotel and its end when the group goes, an administrator group waiting for approval; s64 7/7 after QuarantineReleases went)
- [x] F plumbing (s66 7/7: the outbox table before the first save, the row in the same save with the message id, confirm-mode channels, a crash after the save delivers each event once, order kept through an outage, a poison row set aside after 10 tries with the rows behind it going on and the health check Degraded, Completed rows deleted after 7 days, the inbox by a test receiver 5/5; s60 5/5 on the new design; s65 25/25 with the SCIM duplicate-key 409)
- [x] screens for B to F (web: Users head links to Company directory, Invitations, Approvals and rules; the person page shows the sign-ins and, for a directory person, a note with no role or state actions; console: a Company directory panel on the tenant page with Remove. Arabic for every new text and error code 00129-00141, 11001-11002, 11101-11104. Checks 0, web tests 247/247, console 2/2, both apps build en and ar)
- [x] full regression (27 Oct, tag BF on api-s66): 40 suites, 965 checks, 0 failed (s66 7/7, s65 25/25, s64 7/7, s63 5/5, s62 5/5, s61 14/14, s60 5/5, the gate s56 44/44, every older suite green)
- [x] browser walk (local stack, en and ar, web and console): Users links; Approvals and rules (days 120 refused, 45 kept; approvals on, off waits after "Confirm it is you", the maker cannot approve, a second administrator approves); Company directory (open, the secret once, a pushed group given Front desk at Ritz Amman); Invitations (the directory person's link, Send again); person page (directory note, no role editor, sign-ins); console panel (Remove asks first, then None). Fixed during the walk: the web app had no "Confirm it is you" step, so every guarded change failed once approvals were on (a shared dialog in the shell now asks for the password and sends the change again; company people are told to ask a password administrator); times without a zone (every createdDate) were read as device time, now as UTC; the sign-in rows' attribute clashed with "How they sign in". Web tests 256/256.
