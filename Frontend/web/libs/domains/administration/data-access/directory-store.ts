import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AccessScope,
  DirectoryConnection,
  DirectoryGroup,
  DirectorySecret,
  GroupChoice,
} from '@harmony/administration/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { Paged } from '@harmony/shared/domain';
import { firstValueFrom } from 'rxjs';
import { GROUPS_PAGE_SIZE, myGroupsPath } from './person-store';

export const myDirectoryPath = '/api/Directory/mine';
export const openDirectoryPath = '/api/Directory/Open';
export const newDirectorySecretPath = '/api/Directory/NewSecret';
export const switchDirectoryPath = '/api/Directory/Switch';
export const mapDirectoryGroupPath = '/api/Directory/MapGroup';

/**
 * The customer's company directory (stage E, SCIM 2.0 from Microsoft Entra ID or Okta, the Mews way): open the
 * connection, give the directory its address and secret, switch it, and give each group it pushes a role. The secret
 * comes back once and is kept only in this store's memory until the page leaves. Provided by the page.
 */
@Injectable()
export class DirectoryStore {
  private readonly api = inject(ApiClient);
  private readonly connectionRead = httpResource<Envelope<DirectoryConnection | null>>(() => ({
    url: myDirectoryPath,
  }));
  private readonly groupsWanted = signal(false);
  private readonly hotelGroups = httpResource<Envelope<Paged<GroupChoice>>>(() =>
    this.groupsWanted()
      ? {
          url: myGroupsPath,
          method: 'POST',
          body: { pageNumber: 1, pageSize: GROUPS_PAGE_SIZE },
        }
      : undefined,
  );
  private readonly shown = signal<DirectorySecret | null>(null);

  readonly connection = computed<DirectoryConnection | null>(
    () => this.connectionRead.value()?.data ?? null,
  );
  readonly groups = computed<readonly DirectoryGroup[]>(() => this.connection()?.groups ?? []);
  readonly loading = this.connectionRead.isLoading;
  readonly problem = computed(() => {
    const error = this.connectionRead.error();
    return error === undefined ? null : messageOf(fromError(error));
  });
  /** The customer's hotel groups, for "where" (empty until loadChoices()). */
  readonly groupChoices = computed<readonly GroupChoice[]>(
    () => this.hotelGroups.value()?.data?.results ?? [],
  );
  /** The secret Identity just showed, this once; null otherwise. */
  readonly secret = this.shown.asReadonly();

  reload(): void {
    this.connectionRead.reload();
  }

  loadChoices(): void {
    this.groupsWanted.set(true);
  }

  forgetSecret(): void {
    this.shown.set(null);
  }

  open(intent: IntentKey): Promise<ApiResult<DirectorySecret>> {
    return this.showing(
      firstValueFrom(this.api.post<DirectorySecret>(openDirectoryPath, {}, intent)),
    );
  }

  /** The old secret and every token made from it stop working now. */
  newSecret(intent: IntentKey): Promise<ApiResult<DirectorySecret>> {
    return this.showing(
      firstValueFrom(this.api.put<DirectorySecret>(newDirectorySecretPath, {}, intent)),
    );
  }

  switch(on: boolean, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(switchDirectoryPath, { on }, intent);
  }

  /** The role the group gives and where; no role: the group gives nothing. */
  mapGroup(
    group: DirectoryGroup,
    roleId: string | null,
    scope: AccessScope | null,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(
      mapDirectoryGroupPath,
      {
        id: group.id,
        roleId,
        propertyId: scope?.propertyId ?? null,
        propertyGroupId: scope?.propertyGroupId ?? null,
      },
      intent,
    );
  }

  private async showing(
    call: Promise<ApiResult<DirectorySecret>>,
  ): Promise<ApiResult<DirectorySecret>> {
    const result = await call;
    if (result.kind === 'ok') {
      this.shown.set(result.data);
      this.connectionRead.reload();
    }
    return result;
  }

  private async write(path: string, body: unknown, intent: IntentKey): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(this.api.put(path, body, intent));
    if (result.kind === 'ok') this.connectionRead.reload();
    return result;
  }
}
