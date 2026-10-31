import { httpResource } from '@angular/common/http';
import { Injectable, computed } from '@angular/core';
import { ActorChoice } from '@harmony/platform-operations/domain';
import { messageOf } from '@harmony/platform/errors';
import { Envelope, fromError } from '@harmony/platform/http';
import { PageRequest, Paged } from '@harmony/shared/domain';

export const actorChoicesPath = '/api/UserAccount/get-all';
/** The most Identity hands out in one page; a platform team larger than this searches the trail by name instead. */
export const ACTOR_CHOICES_PAGE_SIZE = 100;
const FIRST_PAGE = 1;

interface ChoicesRequest extends PageRequest {
  readonly searchText: null;
}

/**
 * The platform's own people as the Audit log's actor filter lists them: one read of the first hundred accounts of the
 * signed-in person's tenant (the platform's), shared by the console's filters (architecture 7.8). A customer's people
 * never appear here: the console has no door to them, and the search box finds any actor by name.
 */
@Injectable({ providedIn: 'root' })
export class ActorChoicesStore {
  private readonly page = httpResource<Envelope<Paged<ActorChoice>>>(() => {
    const body: ChoicesRequest = {
      pageNumber: FIRST_PAGE,
      pageSize: ACTOR_CHOICES_PAGE_SIZE,
      searchText: null,
    };
    return { url: actorChoicesPath, method: 'POST', body };
  });

  readonly rows = computed<readonly ActorChoice[]>(() =>
    this.page.hasValue() ? (this.page.value().data?.results ?? []) : [],
  );
  /** True when the platform has more people than one page names. */
  readonly truncated = computed(
    () =>
      this.page.hasValue() && (this.page.value().data?.totalCount ?? 0) > ACTOR_CHOICES_PAGE_SIZE,
  );
  readonly loading = this.page.isLoading;
  readonly problem = computed(() => {
    const error = this.page.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  reload(): void {
    this.page.reload();
  }
}
