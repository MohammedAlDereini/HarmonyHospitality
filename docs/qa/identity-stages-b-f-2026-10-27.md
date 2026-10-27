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
  - When it is on, these actions need a second person: giving an administrator-level role, exempting from two-step, switching company login off, merging accounts, and releasing quarantined people.
  - The person asking must hold a live StepUpAuthorization. The checker is never the maker. A request is void if its target's security version moved.
- **E, directory sync:** Microsoft Graph only. Google comes later.
  - It uses plain HTTP calls with the company login's app and client credentials, so no SDK and no new package.
  - Approved groups map to role codes. Delta queries keep a resume token per group.
  - Each run takes a lease with a fencing token. A late worker is refused.
  - New people arrive quarantined (a new state, Quarantined) until released. Leavers and disabled accounts are suspended.
  - An e-mail that belongs to another customer becomes a MigrationAmbiguity and waits for a person.
- **F, plumbing:**
  - Integration events are written to OutboxMessages in the same SaveChanges as the data, and a background publisher sends them on the framework's EventBus. This replaces the after-commit publishers.
  - InboxMessages gives consumers once-only processing; Identity has no consumer yet.
  - RequestIdempotency stores the receipt gate's answers in SQL, replacing the Redis receipt store.

## Notes from the build

- C: the feed line is written by the Identity database context itself, so no handler can forget it. New accounts start at security version 2 (their roles are set at creation); a new account has no tokens, so it writes no line.
- C: the compaction door takes KeepDays (0 to 30) so tests and the platform can compact now; the hourly job keeps 7 days.
- D: Rejected is added to the ERD's states (pending, approved, void). Requests also keep the change itself (Payload) and the account it is about (TargetUserAccountId), which the ERD did not list.
- D: an approval runs the stored change through its own handler, inside one transaction with the execution record. Every rule of that handler is checked again against the checker. A refusal rolls everything back and the request stays waiting.
- D: the gated changes need ManageUsers, the same permission as the approve door. Switching company login off is gated in code; the suite does not drive it (it needs a company login set up), s61 still covers switching it off with approvals off.

## Ledger

- [x] B1 invitations (s62)
- [x] B2 sessions (s62)
- [x] B3 confirm it is you (s62, s61 company confirm)
- [x] C revocation feed (s63 5/5: one line per bump in the same transaction, six parallel writes without holes, platform-only doors, compaction with snapshot and tombstone)
- [x] D approvals (s64 7/7: off by default, maker confirms first, checker never the maker, void when the account moved, merge rejected then approved, switching off approved too)
- [ ] E directory sync
- [ ] F plumbing
- [ ] full regression
