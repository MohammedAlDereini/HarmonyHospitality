import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AccessScope,
  AccountAccess,
  AccountSignIn,
  CompanyLink,
  Device,
  GroupChoice,
  HotelAccessGrant,
  HotelRoleAssignment,
  Passkey,
  SecurityEvent,
  SessionRecord,
  UserAccount,
} from '@harmony/administration/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { Paged } from '@harmony/shared/domain';
import { Observable, firstValueFrom } from 'rxjs';

export const accountPath = (id: string): string => `/api/UserAccount/${id}`;
export const devicesPath = (id: string): string => `${accountPath(id)}/devices`;
export const passkeysPath = (id: string): string => `${accountPath(id)}/passkeys`;
export const eventsPath = (id: string): string => `${accountPath(id)}/security-events`;
/** Stage B: the person's latest sign-ins (the newest 50). */
export const sessionsPath = (id: string): string => `${accountPath(id)}/sessions`;
export const accessPath = (id: string): string => `${accountPath(id)}/access`;
export const updateRolesPath = '/api/UserAccount/UpdateRoles';
export const resetTwoFactorPath = '/api/UserAccount/ResetTwoFactor';
export const endExemptionPath = '/api/UserAccount/EndTwoStepExemption';
export const signOutDevicePath = '/api/UserAccount/SignOutDevice';
export const removePasskeyPath = '/api/UserAccount/RemovePasskey';
export const assignRolePath = '/api/UserAccount/AssignRole';
export const endAssignmentPath = '/api/UserAccount/EndAssignment';
export const grantAccessPath = '/api/UserAccount/GrantAccess';
export const revokeGrantPath = '/api/UserAccount/RevokeGrant';
export const myGroupsPath = '/api/PropertyGroup/mine';
export const signInPath = (id: string): string => `/api/CompanyLogin/account/${id}`;
export const passwordSignInPath = '/api/CompanyLogin/PasswordSignIn';
export const endLinkPath = '/api/CompanyLogin/EndLink';
/** Identity pages its answers, at most 100 a page; a customer's groups fit in one. */
export const GROUPS_PAGE_SIZE = 100;
const FIRST_PAGE = 1;

/**
 * One person's page (architecture 7.8): the account, its devices, passkeys, security trail and what gives them
 * access at one hotel or group (assignments, grants), each its own resource keyed by the account id, each read
 * again after the write that changes it. Provided by the page, so it lives and dies with it.
 */
@Injectable()
export class PersonStore {
  private readonly api = inject(ApiClient);
  private readonly id = signal<string | null>(null);
  /** The group choices are read only once an editor asks for them. */
  private readonly choicesWanted = signal(false);

  private readonly account = httpResource<Envelope<UserAccount>>(() => {
    const id = this.id();
    return id === null ? undefined : { url: accountPath(id) };
  });
  private readonly devices = httpResource<Envelope<Device[]>>(() => {
    const id = this.id();
    return id === null ? undefined : { url: devicesPath(id) };
  });
  private readonly passkeys = httpResource<Envelope<Passkey[]>>(() => {
    const id = this.id();
    return id === null ? undefined : { url: passkeysPath(id) };
  });
  private readonly sessions = httpResource<Envelope<SessionRecord[]>>(() => {
    const id = this.id();
    return id === null ? undefined : { url: sessionsPath(id) };
  });
  private readonly events = httpResource<Envelope<SecurityEvent[]>>(() => {
    const id = this.id();
    return id === null ? undefined : { url: eventsPath(id) };
  });
  private readonly access = httpResource<Envelope<AccountAccess>>(() => {
    const id = this.id();
    return id === null ? undefined : { url: accessPath(id) };
  });
  private readonly signIn = httpResource<Envelope<AccountSignIn>>(() => {
    const id = this.id();
    return id === null ? undefined : { url: signInPath(id) };
  });
  private readonly groups = httpResource<Envelope<Paged<GroupChoice>>>(() =>
    this.choicesWanted()
      ? {
          url: myGroupsPath,
          method: 'POST',
          body: { pageNumber: FIRST_PAGE, pageSize: GROUPS_PAGE_SIZE },
        }
      : undefined,
  );

  readonly person = computed<UserAccount | null>(() => this.account.value()?.data ?? null);
  readonly loading = this.account.isLoading;
  readonly problem = computed(() => this.sentence(this.account.error()));
  readonly deviceRows = computed<readonly Device[]>(() => this.devices.value()?.data ?? []);
  readonly devicesProblem = computed(() => this.sentence(this.devices.error()));
  readonly passkeyRows = computed<readonly Passkey[]>(() => this.passkeys.value()?.data ?? []);
  readonly passkeysProblem = computed(() => this.sentence(this.passkeys.error()));
  readonly eventRows = computed<readonly SecurityEvent[]>(() => this.events.value()?.data ?? []);
  readonly sessionRows = computed<readonly SessionRecord[]>(
    () => this.sessions.value()?.data ?? [],
  );
  readonly eventsProblem = computed(() => this.sentence(this.events.error()));
  readonly assignmentRows = computed<readonly HotelRoleAssignment[]>(
    () => this.access.value()?.data?.assignments ?? [],
  );
  readonly grantRows = computed<readonly HotelAccessGrant[]>(
    () => this.access.value()?.data?.grants ?? [],
  );
  readonly accessProblem = computed(() => this.sentence(this.access.error()));
  /** How the person signs in (company login or a Harmony password) and their company links; null until read. */
  readonly signInState = computed<AccountSignIn | null>(() => this.signIn.value()?.data ?? null);
  readonly signInProblem = computed(() => this.sentence(this.signIn.error()));
  /** The customer's groups a role may be given at; empty until loadChoices() and while Identity has none. */
  readonly groupRows = computed<readonly GroupChoice[]>(
    () => this.groups.value()?.data?.results ?? [],
  );

  /** The page names the account; a different id starts the reads afresh. */
  open(id: string): void {
    this.id.set(id);
  }

  /** An editor opened: the group choices are wanted now. */
  loadChoices(): void {
    this.choicesWanted.set(true);
  }

  reload(): void {
    this.account.reload();
  }

  /** The account holds exactly these roles afterwards (UpdateUserRolesCommand). */
  updateRoles(
    user: UserAccount,
    roleIds: readonly string[],
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(updateRolesPath, { id: user.id, roleIds }, intent), () => {
      this.account.reload();
      this.events.reload();
    });
  }

  /** Clears the authenticator; the person enrols again at the next sign-in. */
  resetTwoFactor(user: UserAccount, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(resetTwoFactorPath, { id: user.id }, intent), () => {
      this.account.reload();
      this.events.reload();
    });
  }

  endExemption(user: UserAccount, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(endExemptionPath, { id: user.id }, intent), () => {
      this.account.reload();
      this.events.reload();
    });
  }

  signOutDevice(user: UserAccount, device: Device, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(
      this.api.put(signOutDevicePath, { id: user.id, deviceId: device.id }, intent),
      () => {
        this.devices.reload();
        this.sessions.reload();
        this.events.reload();
      },
    );
  }

  removePasskey(
    user: UserAccount,
    passkey: Passkey,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(
      this.api.put(removePasskeyPath, { id: user.id, passkeyId: passkey.id }, intent),
      () => {
        this.passkeys.reload();
        this.events.reload();
      },
    );
  }

  /** A role at one hotel or one group, open-ended or until an instant (AssignRoleCommand). */
  assignRole(
    user: UserAccount,
    roleId: string,
    scope: AccessScope,
    endsAtUtc: string | null,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(
      this.api.put(assignRolePath, { id: user.id, roleId, ...scope, endsAtUtc }, intent),
      () => {
        this.access.reload();
        this.events.reload();
      },
    );
  }

  endAssignment(
    user: UserAccount,
    assignment: HotelRoleAssignment,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(
      this.api.put(endAssignmentPath, { id: user.id, assignmentId: assignment.id }, intent),
      () => {
        this.access.reload();
        this.events.reload();
      },
    );
  }

  /** A role lent from now until an instant at most 30 days away, with the reason (GrantAccessCommand). */
  grantAccess(
    user: UserAccount,
    roleId: string,
    scope: AccessScope,
    toUtc: string,
    reason: string,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(
      this.api.post(grantAccessPath, { id: user.id, roleId, ...scope, toUtc, reason }, intent),
      () => {
        this.access.reload();
        this.events.reload();
      },
    );
  }

  revokeGrant(
    user: UserAccount,
    grant: HotelAccessGrant,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(
      this.api.put(revokeGrantPath, { id: user.id, grantId: grant.id }, intent),
      () => {
        this.access.reload();
        this.events.reload();
      },
    );
  }

  /** Keep the person on a Harmony password (true), or give them to the company login (false). */
  setPasswordSignIn(
    user: UserAccount,
    keep: boolean,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(
      this.api.put(passwordSignInPath, { id: user.id, passwordSignIn: keep }, intent),
      () => {
        this.signIn.reload();
        this.events.reload();
      },
    );
  }

  /** That company identity cannot sign in to this account again; the person's sessions end. */
  endLink(user: UserAccount, link: CompanyLink, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(endLinkPath, { id: user.id, linkId: link.id }, intent), () => {
      this.signIn.reload();
      this.devices.reload();
      this.sessions.reload();
      this.events.reload();
    });
  }

  private sentence(error: unknown): string | null {
    return error === undefined ? null : messageOf(fromError(error));
  }

  /** A write that succeeded changed something: read it again. The outcome goes back to the screen as it is. */
  private async write(
    call: Observable<ApiResult<unknown>>,
    after: () => void,
  ): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(call);
    if (result.kind === 'ok') after();
    return result;
  }
}
