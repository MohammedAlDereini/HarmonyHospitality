import { httpResource } from '@angular/common/http';
import { Injectable, computed, signal } from '@angular/core';
import { RoleOption } from '@harmony/administration/domain';
import { messageOf } from '@harmony/platform/errors';
import { Envelope, fromError } from '@harmony/platform/http';
import { PageRequest, Paged } from '@harmony/shared/domain';

export const rolesPath = '/api/Role/get-all';
/** Identity accepts 1 to 100 rows a page; a tenant's roles fit in one. */
export const ROLES_PAGE_SIZE = 100;
const FIRST_PAGE = 1;

/** The request Identity reads (GetPagedRolesQuery). */
interface RolesRequest extends PageRequest {
  readonly searchText: string | null;
}

/**
 * The tenant's roles, for choosing one (architecture 7.8): read only when a screen asks, read again each time it
 * asks, so a role created meanwhile is there. Roles are tenant-wide; a property switch does not change them.
 */
@Injectable({ providedIn: 'root' })
export class RolesStore {
  private readonly wanted = signal(false);

  private readonly page = httpResource<Envelope<Paged<RoleOption>>>(() => {
    if (!this.wanted()) return undefined;
    const body: RolesRequest = {
      pageNumber: FIRST_PAGE,
      pageSize: ROLES_PAGE_SIZE,
      searchText: null,
    };
    return { url: rolesPath, method: 'POST', body };
  });

  readonly options = computed<readonly RoleOption[]>(() => this.page.value()?.data?.results ?? []);
  readonly loading = this.page.isLoading;
  /** One sentence when the read failed, in the person's language; null while all is well. */
  readonly problem = computed(() => {
    const error = this.page.error();
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
}
