import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { PersonStore } from '@harmony/administration/data-access';
import { Device, Passkey, UserAccount } from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { InstantFormatter } from '@harmony/platform/context';
import { messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { Tone } from '@harmony/ui/primitives';
import { AccountStateEditor } from './account-state-editor';
import { stateLabel, toneOf, twoStepLabel } from './account-wording';
import { RoleAssignment } from './role-assignment';

const dateAndTime: Intl.DateTimeFormatOptions = { dateStyle: 'medium', timeStyle: 'short' };

/**
 * One account: who they are, the roles they hold, how they sign in (two-step, exemption), the devices that signed
 * in, their passkeys, and the security trail (architecture 6, administration). Every write is one press with its
 * own intent key; the store reads the changed part again.
 */
@Component({
  selector: 'hr-person-page',
  imports: [IfAllowed, RoleAssignment, AccountStateEditor],
  providers: [PersonStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './person-page.html',
  styleUrl: './person-page.css',
})
export class PersonPage {
  /** From the route (:id), bound by the router. */
  readonly id = input.required<string>();

  protected readonly store = inject(PersonStore);
  private readonly context = inject(InstantFormatter);

  protected readonly manageUsers = Permission.ManageUsers;
  /** True while the exemption reason form is open. */
  protected readonly exempting = signal(false);
  /** True while a one-press write is in flight. */
  protected readonly busy = signal(false);
  /** One sentence when a write was refused; null while all is well. */
  protected readonly actionProblem = signal<string | null>(null);
  protected readonly deviceCount = computed(() => this.store.deviceRows().length);
  protected readonly passkeyCount = computed(() => this.store.passkeyRows().length);
  protected readonly eventCount = computed(() => this.store.eventRows().length);

  constructor() {
    effect(() => {
      this.store.open(this.id());
    });
  }

  protected stateOf(user: UserAccount): string {
    return stateLabel(user);
  }

  protected tone(user: UserAccount): Tone | null {
    return toneOf(user);
  }

  protected twoStepOf(user: UserAccount): string {
    return twoStepLabel(user);
  }

  protected when(instant: string): string {
    return this.context.formatInstant(instant, dateAndTime);
  }

  protected openExempt(): void {
    this.actionProblem.set(null);
    this.exempting.set(true);
  }

  protected closeExempt(): void {
    this.exempting.set(false);
  }

  protected onExempted(): void {
    this.exempting.set(false);
    this.store.reload();
  }

  protected resetTwoFactor(user: UserAccount): void {
    void this.press(this.store.resetTwoFactor(user, newIntentKey()));
  }

  protected endExemption(user: UserAccount): void {
    void this.press(this.store.endExemption(user, newIntentKey()));
  }

  protected signOut(user: UserAccount, device: Device): void {
    void this.press(this.store.signOutDevice(user, device, newIntentKey()));
  }

  protected removePasskey(user: UserAccount, passkey: Passkey): void {
    void this.press(this.store.removePasskey(user, passkey, newIntentKey()));
  }

  /** One press: nothing else until it settles; a refusal is worded once. */
  private async press(call: Promise<ApiResult<unknown>>): Promise<void> {
    this.actionProblem.set(null);
    this.busy.set(true);
    const result = await call;
    this.busy.set(false);
    if (result.kind !== 'ok') this.actionProblem.set(messageOf(result));
  }
}
