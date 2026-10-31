import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PersonStore, RolesStore, UsersStore } from '@harmony/administration/data-access';
import {
  AccountState,
  CompanyLink,
  Device,
  HotelAccessGrant,
  HotelRoleAssignment,
  Passkey,
  UserAccount,
  roleName,
} from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { initialsOf } from '@harmony/shared/domain';
import { Icon } from '@harmony/ui/icons';
import { ConfirmDialog, Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead, PopAt } from '@harmony/ui/shell';
import {
  AccountAction,
  AccountStateEditor,
  HotelAccessEditor,
  RoleAssignment,
  stateLabel,
} from '@harmony/administration/feature-users';
import { dayUtc, stampUtc } from './stamps';

/** The page's tabs (C18). */
export type PersonTab = 'overview' | 'devices' | 'passkeys' | 'sign-ins' | 'trail' | 'hotel-roles';
const tabs: readonly PersonTab[] = [
  'overview',
  'devices',
  'passkeys',
  'sign-ins',
  'trail',
  'hotel-roles',
];
const isTab = (value: string): value is PersonTab => (tabs as readonly string[]).includes(value);

/** The chip's tone beside a word (never colour alone). */
type ChipTone = 'ok' | 'warn' | 'stop' | 'vip' | null;
const stateTone: Readonly<Record<AccountState, ChipTone>> = {
  [AccountState.active]: 'ok',
  [AccountState.suspended]: 'warn',
  [AccountState.terminated]: 'stop',
};
const drawerHeading: Readonly<Record<AccountAction, string>> = {
  suspend: $localize`:@@administration.users.drawer.suspend:Suspend account`,
  terminate: $localize`:@@administration.users.drawer.terminate:Terminate account`,
  exempt: $localize`:@@administration.users.drawer.exempt:Exempt from two-step`,
};
const doneToast: Readonly<Record<AccountAction, string>> = {
  suspend: $localize`:@@administration.users.toast.suspended:Account suspended.`,
  terminate: $localize`:@@administration.users.toast.terminated:Account terminated.`,
  exempt: $localize`:@@administration.users.toast.exempted:Exempted from two-step.`,
};
/** How Identity names a link made by the person's own first company sign-in. */
const FIRST_SIGN_IN = 'first-sign-in';

/**
 * One platform person (platform console design, C18): the head says who they are, how they sign in and when they last
 * did; the overview holds their roles (the real editor), the sign-in facts with their doors, the access lent for a
 * while and the devices; the other tabs list passkeys, sign-ins, the security trail and the roles held at a hotel or
 * group. Every write is one press with its own intent key; the store reads the changed part again.
 */
@Component({
  selector: 'hr-person-console-page',
  imports: [
    NgTemplateOutlet,
    RouterLink,
    IfAllowed,
    Icon,
    PageHead,
    PopAt,
    Drawer,
    ConfirmDialog,
    RoleAssignment,
    AccountStateEditor,
    HotelAccessEditor,
  ],
  providers: [PersonStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './person-console-page.html',
  styleUrl: './person-console-page.css',
})
export class PersonConsolePage {
  /** From the route (:id), bound by the router. */
  readonly id = input.required<string>();
  /** ?tab=: the tab to open on. */
  readonly tab = input<string>();

  protected readonly store = inject(PersonStore);
  private readonly roles = inject(RolesStore);
  private readonly users = inject(UsersStore);
  private readonly toasts = inject(Toasts);
  private readonly stateDrawer = viewChild.required<Drawer>('stateDrawer');
  private readonly assignDrawer = viewChild.required<Drawer>('assignDrawer');
  private readonly grantDrawer = viewChild.required<Drawer>('grantDrawer');
  private readonly endDialog = viewChild.required<ConfirmDialog>('endDialog');
  private readonly revokeDialog = viewChild.required<ConfirmDialog>('revokeDialog');
  private readonly endLinkDialog = viewChild.required<ConfirmDialog>('endLinkDialog');
  private readonly rolesCard = viewChild<ElementRef<HTMLElement>>('rolesCard');
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageUsers = Permission.ManageUsers;
  protected readonly assignRoles = Permission.AssignUsersToRole;
  protected readonly current = signal<PersonTab>('overview');
  protected readonly editing = signal<AccountAction | null>(null);
  protected readonly assigning = signal(false);
  protected readonly granting = signal(false);
  protected readonly busy = signal(false);
  protected readonly actionProblem = signal<string | null>(null);
  protected readonly stateHeading = computed(() => {
    const editing = this.editing();
    return editing === null ? '' : drawerHeading[editing];
  });
  protected readonly trustedDevices = computed(
    () => this.store.deviceRows().filter((device) => device.trustedAtUtc !== null).length,
  );
  protected readonly activeGrants = computed(() =>
    this.store.grantRows().filter((grant) => grant.isActive),
  );
  protected readonly currentAssignments = computed(
    () => this.store.assignmentRows().filter((assignment) => assignment.isCurrent).length,
  );

  constructor() {
    this.roles.load();
    effect(() => {
      this.store.open(this.id());
    });
    effect(() => {
      const wanted = this.tab();
      untracked(() => {
        if (wanted !== undefined && isTab(wanted)) this.current.set(wanted);
      });
    });
  }

  // ── the head ────────────────────────────────────────────────────────────────

  protected initials(person: UserAccount): string {
    return initialsOf(person.displayName);
  }

  protected stateOf(person: UserAccount): string {
    return stateLabel(person);
  }

  protected stateTone(person: UserAccount): ChipTone {
    return stateTone[person.state];
  }

  protected twoStepWord(person: UserAccount): string {
    if (person.twoStepExemptReason !== null)
      return $localize`:@@administration.console.person.two-step.exempt:Two-step exempt`;
    return person.twoFactorEnabled
      ? $localize`:@@administration.console.person.two-step.on:Two-step on`
      : $localize`:@@administration.console.person.two-step.off:Two-step not set`;
  }

  protected twoStepTone(person: UserAccount): ChipTone {
    if (person.twoStepExemptReason !== null) return 'vip';
    return person.twoFactorEnabled ? 'ok' : 'warn';
  }

  protected rolesOf(person: UserAccount): string {
    return this.roles
      .roles()
      .filter((role) => person.roleIds.includes(role.id))
      .map((role) => roleName(role, this.locale))
      .join(', ');
  }

  /** "Harmony password" or "Company account (Microsoft)"; '' until the sign-in state is read. */
  protected signInWord(): string {
    const signIn = this.store.signInState();
    if (signIn === null) return '';
    if (signIn.signsInWith === 'Company')
      return $localize`:@@administration.console.person.sign-in.company:Company account (${signIn.coveredBy ?? ''}:provider:)`;
    return $localize`:@@administration.console.person.sign-in.password:Harmony password`;
  }

  protected lastSignIn(person: UserAccount): string {
    if (person.lastSignInUtc === null)
      return $localize`:@@administration.console.person.never:never signed in`;
    const at = stampUtc(person.lastSignInUtc);
    return person.lastSignInDevice === null
      ? $localize`:@@administration.console.person.last-sign-in:last sign-in ${at}:when:`
      : $localize`:@@administration.console.person.last-sign-in-from:last sign-in ${at}:when: from ${person.lastSignInDevice}:device:`;
  }

  protected show(tab: PersonTab): void {
    this.current.set(tab);
  }

  /** Edit roles: the overview's roles card is the editor; bring it into view. */
  protected editRoles(): void {
    this.current.set('overview');
    this.rolesCard()?.nativeElement.scrollIntoView({ block: 'start', behavior: 'smooth' });
    this.rolesCard()?.nativeElement.querySelector<HTMLInputElement>('input')?.focus();
  }

  // ── sign-in ─────────────────────────────────────────────────────────────────

  protected twoStepFact(person: UserAccount): string {
    if (person.twoStepExemptReason !== null)
      return $localize`:@@administration.console.person.fact.exempt:On the exception list: ${person.twoStepExemptReason}:reason:`;
    return person.twoFactorEnabled
      ? $localize`:@@administration.console.person.fact.two-step-on:Set up; a code is asked at each sign-in`
      : $localize`:@@administration.console.person.fact.two-step-off:Not set up yet; asked at the next sign-in`;
  }

  protected passwordFact(): string {
    const signIn = this.store.signInState();
    if (signIn === null) return '…';
    if (signIn.signsInWith === 'Company')
      return $localize`:@@administration.console.person.fact.no-password:None; the company account signs them in`;
    return signIn.passwordSignIn
      ? $localize`:@@administration.console.person.fact.password-kept:Harmony password, kept by an administrator`
      : $localize`:@@administration.console.person.fact.password:Harmony password`;
  }

  protected linkWord(link: CompanyLink): string {
    const by =
      link.linkedBy === FIRST_SIGN_IN
        ? $localize`:@@administration.console.person.link.first:on their first company sign-in`
        : $localize`:@@administration.console.person.link.admin:by an administrator`;
    return `${link.provider} · ${by} · ${dayUtc(link.since)}`;
  }

  protected stamp(instant: string | null): string {
    return stampUtc(instant);
  }

  protected day(instant: string | null): string {
    return dayUtc(instant);
  }

  // ── access for a while ──────────────────────────────────────────────────────

  protected grantTitle(grant: HotelAccessGrant): string {
    return $localize`:@@administration.console.person.grant.at:${grant.roleCode}:role: at ${grant.scopeCode}:scope:`;
  }

  protected grantUntil(grant: HotelAccessGrant): string {
    return $localize`:@@administration.console.person.grant.until:until ${stampUtc(grant.toUtc)}:when:`;
  }

  /** "Omar Haddad · Oct 27, 2026": the platform person who lent the access. */
  protected givenBy(grant: HotelAccessGrant): string {
    const who = this.users.rows().find((row) => row.id === grant.grantedById)?.displayName ?? '—';
    return `${who} · ${dayUtc(grant.fromUtc)}`;
  }

  protected grantState(grant: HotelAccessGrant): string {
    if (grant.isActive) return $localize`:@@administration.console.person.grant.active:Active`;
    if (grant.revokedAtUtc !== null)
      return $localize`:@@administration.console.person.grant.revoked:Revoked`;
    return $localize`:@@administration.console.person.grant.over:Over`;
  }

  protected whereWord(row: HotelRoleAssignment | HotelAccessGrant): string {
    return row.propertyId !== null
      ? $localize`:@@administration.console.person.where.hotel:Hotel`
      : $localize`:@@administration.console.person.where.group:Group`;
  }

  // ── the doors ───────────────────────────────────────────────────────────────

  protected openState(mode: AccountAction): void {
    this.actionProblem.set(null);
    this.editing.set(mode);
    this.stateDrawer().open();
  }

  protected stateDone(): void {
    const editing = this.editing();
    if (editing !== null) this.toasts.show(doneToast[editing]);
    this.stateDrawer().close();
    this.store.reload();
  }

  protected openAssign(): void {
    this.actionProblem.set(null);
    this.assigning.set(true);
    this.assignDrawer().open();
  }

  protected onAssigned(): void {
    this.toasts.show($localize`:@@administration.person.toast.assigned:Role given.`);
    this.assignDrawer().close();
  }

  protected openGrant(): void {
    this.actionProblem.set(null);
    this.granting.set(true);
    this.grantDrawer().open();
  }

  protected onGranted(): void {
    this.toasts.show($localize`:@@administration.person.toast.granted:Access granted for a while.`);
    this.grantDrawer().close();
  }

  protected reinstate(person: UserAccount): void {
    void this.press(this.users.reinstate(person, newIntentKey()), () => {
      this.toasts.show($localize`:@@administration.users.toast.reinstated:Account reinstated.`);
      this.store.reload();
    });
  }

  protected resetTwoFactor(person: UserAccount): void {
    void this.press(this.store.resetTwoFactor(person, newIntentKey()), () => {
      this.toasts.show(
        $localize`:@@administration.person.toast.two-step-reset:Two-step reset: the person sets it up again at the next sign-in.`,
      );
    });
  }

  protected endExemption(person: UserAccount): void {
    void this.press(this.store.endExemption(person, newIntentKey()), () => {
      this.toasts.show(
        $localize`:@@administration.person.toast.exemption-ended:Exemption ended: two-step applies again.`,
      );
    });
  }

  /** The administrator keeps the person on a Harmony password, whatever the company login covers. */
  protected keepPassword(person: UserAccount): void {
    void this.press(this.store.setPasswordSignIn(person, true, newIntentKey()), () => {
      this.toasts.show(
        $localize`:@@administration.person.toast.kept-password:Kept on a Harmony password.`,
      );
    });
  }

  protected followCompany(person: UserAccount): void {
    void this.press(this.store.setPasswordSignIn(person, false, newIntentKey()), () => {
      this.toasts.show(
        $localize`:@@administration.person.toast.company-sign-in:The company sign-in applies from the next sign-in.`,
      );
    });
  }

  protected async endLink(person: UserAccount, link: CompanyLink): Promise<void> {
    if (!(await this.endLinkDialog().ask())) return;
    await this.press(this.store.endLink(person, link, newIntentKey()), () => {
      this.toasts.show($localize`:@@administration.person.toast.link-ended:Company link ended.`);
    });
  }

  protected signOut(person: UserAccount, device: Device): void {
    void this.press(this.store.signOutDevice(person, device, newIntentKey()), () => {
      this.toasts.show(
        $localize`:@@administration.person.toast.device-signed-out:Signed out of that device.`,
      );
    });
  }

  protected removePasskey(person: UserAccount, passkey: Passkey): void {
    void this.press(this.store.removePasskey(person, passkey, newIntentKey()), () => {
      this.toasts.show($localize`:@@administration.person.toast.passkey-removed:Passkey removed.`);
    });
  }

  /** Ending stops the role at the next call; the row stays, ended, as part of the record. */
  protected async endAssignment(
    person: UserAccount,
    assignment: HotelRoleAssignment,
  ): Promise<void> {
    if (!(await this.endDialog().ask())) return;
    await this.press(this.store.endAssignment(person, assignment, newIntentKey()), () => {
      this.toasts.show($localize`:@@administration.person.toast.role-ended:Role ended.`);
    });
  }

  protected async revokeGrant(person: UserAccount, grant: HotelAccessGrant): Promise<void> {
    if (!(await this.revokeDialog().ask())) return;
    await this.press(this.store.revokeGrant(person, grant, newIntentKey()), () => {
      this.toasts.show($localize`:@@administration.person.toast.access-revoked:Access revoked.`);
    });
  }

  /** One press: nothing else until it settles; a refusal is worded once. */
  private async press(call: Promise<ApiResult<unknown>>, then?: () => void): Promise<void> {
    this.actionProblem.set(null);
    this.busy.set(true);
    const result = await call;
    this.busy.set(false);
    if (result.kind !== 'ok') {
      this.actionProblem.set(messageOf(result));
      return;
    }
    then?.();
  }
}
