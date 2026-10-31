import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  BrandDraft,
  GroupDraft,
  GroupFilters,
  GroupUpdate,
  Property,
  PropertyGroup,
} from '@harmony/platform-admin/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiClient, ApiResult, Envelope, IntentKey, fromError } from '@harmony/platform/http';
import { PageRequest, Paged } from '@harmony/shared/domain';
import { Observable, firstValueFrom } from 'rxjs';

export const groupsPath = '/api/PropertyGroup/get-all';
export const createGroupPath = '/api/PropertyGroup/Create';
export const updateGroupPath = '/api/PropertyGroup/Update';
export const addMemberPath = '/api/PropertyGroup/AddMember';
export const endMembershipPath = '/api/PropertyGroup/EndMembership';
export const groupPath = (id: string): string => `/api/PropertyGroup/${id}`;
const hotelsPath = '/api/Property/get-all';
/** Identity accepts 1 to 100 rows a page. */
export const GROUPS_PAGE_SIZE = 25;
const HOTEL_CHOICES_SIZE = 100;
const FIRST_PAGE = 1;
const noFilters: GroupFilters = {
  tenantId: null,
  typeCode: null,
  isActive: null,
  search: '',
  page: FIRST_PAGE,
};

/** The request Identity reads (GetPagedPropertyGroupsQuery). */
interface GroupsRequest extends PageRequest {
  readonly tenantId: string | null;
  readonly typeCode: string | null;
  readonly searchText: string | null;
  readonly isActive: boolean | null;
}

const blankToNull = (text: string): string | null => (text.trim() === '' ? null : text.trim());

/** The brand as Identity reads it: blanks as nulls, or nothing at all. */
const brandBody = (brand: BrandDraft | null): Record<string, unknown> | null =>
  brand === null
    ? null
    : {
        themeReference: blankToNull(brand.themeReference),
        logoFileId: null,
        websiteUrl: blankToNull(brand.websiteUrl),
        franchisorName: blankToNull(brand.franchisorName),
        agreementStartsOn: blankToNull(brand.agreementStartsOn),
        agreementEndsOn: blankToNull(brand.agreementEndsOn),
        royaltyPercent: brand.royaltyPercent,
      };

/** Every customer's hotel groups (architecture 7.8): one resource keyed by the filters; writes read the list again. */
@Injectable({ providedIn: 'root' })
export class GroupsStore {
  private readonly api = inject(ApiClient);
  readonly filters = signal<GroupFilters>(noFilters);

  private readonly page = httpResource<Envelope<Paged<PropertyGroup>>>(() => {
    const filters = this.filters();
    const body: GroupsRequest = {
      pageNumber: filters.page,
      pageSize: GROUPS_PAGE_SIZE,
      tenantId: filters.tenantId,
      typeCode: filters.typeCode,
      searchText: filters.search === '' ? null : filters.search,
      isActive: filters.isActive,
    };
    return { url: groupsPath, method: 'POST', body };
  });

  readonly rows = computed<readonly PropertyGroup[]>(() => this.page.value()?.data?.results ?? []);
  readonly total = computed(() => this.page.value()?.data?.totalCount ?? 0);
  readonly loading = this.page.isLoading;
  readonly problem = computed(() => {
    const error = this.page.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  search(text: string): void {
    this.filters.update((filters) => ({ ...filters, search: text.trim(), page: FIRST_PAGE }));
  }

  ofTenant(tenantId: string | null): void {
    this.filters.update((filters) => ({ ...filters, tenantId, page: FIRST_PAGE }));
  }

  ofType(typeCode: string | null): void {
    this.filters.update((filters) => ({ ...filters, typeCode, page: FIRST_PAGE }));
  }

  /** Only active (true) or only inactive (false) groups; null shows both. */
  showState(isActive: boolean | null): void {
    this.filters.update((filters) => ({ ...filters, isActive, page: FIRST_PAGE }));
  }

  goTo(page: number): void {
    this.filters.update((filters) => ({ ...filters, page }));
  }

  reload(): void {
    this.page.reload();
  }

  detail(id: string): Promise<ApiResult<PropertyGroup>> {
    return firstValueFrom(this.api.get<PropertyGroup>(groupPath(id)));
  }

  /** The customer's active hotels, for the Add-hotel drawer (the first hundred; a chain is smaller than that). */
  async hotelsOf(tenantId: string): Promise<readonly Property[]> {
    const body = {
      pageNumber: FIRST_PAGE,
      pageSize: HOTEL_CHOICES_SIZE,
      tenantId,
      searchText: null,
      isActive: true,
    };
    const result = await firstValueFrom(this.api.post<Paged<Property>>(hotelsPath, body));
    return result.kind === 'ok' ? result.data.results : [];
  }

  create(draft: GroupDraft, intent: IntentKey): Promise<ApiResult<unknown>> {
    const body = {
      ...draft,
      description: blankToNull(draft.description),
      externalIdentifier: blankToNull(draft.externalIdentifier),
      brand: brandBody(draft.brand),
    };
    return this.write(this.api.post(createGroupPath, body, intent));
  }

  update(
    group: PropertyGroup,
    update: GroupUpdate,
    intent: IntentKey,
  ): Promise<ApiResult<unknown>> {
    const body = {
      id: group.id,
      ...update,
      description: blankToNull(update.description),
      externalIdentifier: blankToNull(update.externalIdentifier),
      brand: brandBody(update.brand),
    };
    return this.write(this.api.put(updateGroupPath, body, intent));
  }

  addMember(groupId: string, propertyId: string, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(
      this.api.post(addMemberPath, { propertyGroupId: groupId, propertyId, fromUtc: null }, intent),
    );
  }

  endMembership(memberId: string, intent: IntentKey): Promise<ApiResult<unknown>> {
    return this.write(this.api.put(endMembershipPath, { memberId, toUtc: null }, intent));
  }

  private async write(call: Observable<ApiResult<unknown>>): Promise<ApiResult<unknown>> {
    const result = await firstValueFrom(call);
    if (result.kind === 'ok') this.page.reload();
    return result;
  }
}
