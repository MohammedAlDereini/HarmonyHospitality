import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { PermissionEntry, Role, RoleDetail, RoleDraft } from '@harmony/administration/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { PageRequest, Paged } from '@harmony/shared/domain';
import { Observable, firstValueFrom } from 'rxjs';

export const rolesPath = '/api/Role/get-all';
export const permissionsPath = '/api/Role/permissions';
export const createRolePath = '/api/Role/Create';
export const updateRolePath = '/api/Role/Update';
export const rolePermissionsPath = '/api/Role/UpdatePermissions';
/** GET reads one role with its permissions; DELETE removes it. */
export const rolePath = (id: string): string => `/api/Role/${id}`;
/** Identity accepts 1 to 100 rows a page; a tenant's roles fit in one. */
export const ROLES_PAGE_SIZE = 100;
const FIRST_PAGE = 1;

/** The request Identity reads (GetPagedRolesQuery). */
interface RolesRequest extends PageRequest {
  readonly searchText: string | null;
}

/**
 * The tenant's roles and the permission catalogue (architecture 7.8): each read only when a screen asks, read
 * again each time it asks. Roles are tenant-wide; a property switch does not change them. Every write carries the
 * press's intent key and reads the list again on success.
 */
@Injectable({ providedIn: 'root' })
export class RolesStore {
  private readonly api = inject(ApiClient);
  private readonly wanted = signal(false);
  private readonly catalogueWanted = signal(false);

  private readonly page = httpResource<Envelope<Paged<Role>>>(() => {
    if (!this.wanted()) return undefined;
    const body: RolesRequest = {
      pageNumber: FIRST_PAGE,
      pageSize: ROLES_PAGE_SIZE,
      searchText: null,
    };
    return { url: rolesPath, method: 'POST', body };
  });

  private readonly catalogue = httpResource<Envelope<PermissionEntry[]>>(() =>
    this.catalogueWanted() ? { url: permissionsPath } : undefined,
  );

  readonly roles = computed<readonly Role[]>(() => this.page.value()?.data?.results ?? []);
  readonly loading = this.page.isLoading;
  /** One sentence when the read failed, in the person's language; null while all is well. */
  readonly problem = computed(() => {
    const error = this.page.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  readonly permissions = computed<readonly PermissionEntry[]>(
    () => this.catalogue.value()?.data ?? [],
  );
  readonly catalogueLoading = this.catalogue.isLoading;
  readonly catalogueProblem = computed(() => {
    const error = this.catalogue.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  /** A screen needs the roles now: the first call starts the read, every later one reads again. */
  load(): void {
    if (this.wanted()) {
      this.page.reload();
      return;
    }
    this.wanted.set(true);
  }

  /** The permission catalogue, the same way. */
  loadCatalogue(): void {
    if (this.catalogueWanted()) {
      this.catalogue.reload();
      return;
    }
    this.catalogueWanted.set(true);
  }

  /** One role with the permissions it holds. */
  detail(role: Role): Promise<ApiResult<RoleDetail>> {
    return firstValueFrom(this.api.get<RoleDetail>(rolePath(role.id)));
  }

  create(
    draft: RoleDraft,
    permissionIds: readonly string[],
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(
      this.api.post(createRolePath, { ...this.body(draft), permissionIds }, intent),
    );
  }

  update(role: Role, draft: RoleDraft, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(updateRolePath, { id: role.id, ...this.body(draft) }, intent));
  }

  remove(role: Role, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.delete(rolePath(role.id), intent));
  }

  /** The role holds exactly these permissions afterwards. */
  setPermissions(
    role: Role,
    permissionIds: readonly string[],
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    return this.write(
      this.api.post(rolePermissionsPath, { roleId: role.id, permissionIds }, intent),
    );
  }

  /** Identity wants null, not an empty string, for a name that was not given. */
  private body(draft: RoleDraft): Record<string, unknown> {
    return {
      code: draft.code,
      nameEn: draft.nameEn,
      nameAr: draft.nameAr === '' ? null : draft.nameAr,
      privilegeLevel: draft.privilegeLevel,
    };
  }

  /** A write that succeeded changes the list; read it again. The outcome goes back to the screen as it is. */
  private async write(call: Observable<ApiResult<unknown>>): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(call);
    if (result.kind === 'ok') this.page.reload();
    return result;
  }
}
