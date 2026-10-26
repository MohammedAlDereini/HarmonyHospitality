import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { GroupDraft, PropertyGroup } from '@harmony/platform-admin/domain';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import {
  GROUPS_PAGE_SIZE,
  GroupsStore,
  addMemberPath,
  createGroupPath,
  endMembershipPath,
  groupsPath,
} from './groups-store';

const PRECEDENCE = 10;
const ROYALTY = 5.5;
const AFTER_CREATE = 2;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

const ritzCarlton: PropertyGroup = {
  id: 'g1',
  tenantId: 't1',
  typeCode: 'Brand',
  code: 'RITZ-CARLTON',
  externalIdentifier: null,
  nameEn: 'The Ritz-Carlton',
  nameAr: 'ريتز كارلتون',
  description: null,
  precedence: PRECEDENCE,
  displayOrder: 1,
  isActive: true,
  currentMembers: 0,
  brand: null,
  members: [],
};
const draft: GroupDraft = {
  tenantId: 't1',
  typeCode: 'Brand',
  code: 'MARRIOTT',
  nameEn: 'Marriott',
  nameAr: 'ماريوت',
  description: '',
  precedence: 0,
  displayOrder: 0,
  externalIdentifier: ' ',
  brand: {
    themeReference: '',
    websiteUrl: 'https://www.marriott.com',
    franchisorName: 'Marriott International',
    agreementStartsOn: '',
    agreementEndsOn: '',
    royaltyPercent: ROYALTY,
  },
};

describe('GroupsStore', () => {
  let store: GroupsStore;
  let http: HttpTestingController;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
      ],
    });
    store = TestBed.inject(GroupsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('asks for every group first; a tenant or a type narrows the list from page one', async () => {
    await settle();
    const request = http.expectOne(groupsPath);
    expect(request.request.body).toEqual({
      pageNumber: 1,
      pageSize: GROUPS_PAGE_SIZE,
      tenantId: null,
      typeCode: null,
      searchText: null,
      isActive: null,
    });
    request.flush({ data: { results: [ritzCarlton], totalCount: 1 } });
    await settle();
    expect(store.rows()).toEqual([ritzCarlton]);
    store.ofType('Brand');
    await settle();
    expect(http.expectOne(groupsPath).request.body).toMatchObject({
      typeCode: 'Brand',
      pageNumber: 1,
    });
  });

  it('create sends blanks as nulls and the brand as Identity reads it; the key travels in the header', async () => {
    await settle();
    http.expectOne(groupsPath).flush({ data: { results: [ritzCarlton], totalCount: 1 } });
    const intent = newIntentKey();
    const created = store.create(draft, intent);
    const create = http.expectOne(createGroupPath);
    expect(create.request.body).toMatchObject({
      code: 'MARRIOTT',
      description: null,
      externalIdentifier: null,
      brand: {
        themeReference: null,
        logoFileId: null,
        websiteUrl: 'https://www.marriott.com',
        franchisorName: 'Marriott International',
        agreementStartsOn: null,
        agreementEndsOn: null,
        royaltyPercent: ROYALTY,
      },
    });
    expect(create.request.headers.get('Idempotency-Key')).toBe(intent.value);
    create.flush({ data: 'g2' });
    expect((await created).kind).toBe('ok');
    await settle();
    http
      .expectOne(groupsPath)
      .flush({ data: { results: [ritzCarlton], totalCount: AFTER_CREATE } });
  });

  it('a hotel joins and leaves with the ids Identity names; the list is read again after each', async () => {
    await settle();
    http.expectOne(groupsPath).flush({ data: { results: [ritzCarlton], totalCount: 1 } });
    const joined = store.addMember('g1', 'p1', newIntentKey());
    const join = http.expectOne(addMemberPath);
    expect(join.request.body).toEqual({ propertyGroupId: 'g1', propertyId: 'p1', fromUtc: null });
    join.flush({ data: 'm1' });
    expect((await joined).kind).toBe('ok');
    await settle();
    http.expectOne(groupsPath).flush({ data: { results: [ritzCarlton], totalCount: 1 } });
    const ended = store.endMembership('m1', newIntentKey());
    const end = http.expectOne(endMembershipPath);
    expect(end.request.method).toBe('PUT');
    expect(end.request.body).toEqual({ memberId: 'm1', toUtc: null });
    end.flush({ data: 1 });
    expect((await ended).kind).toBe('ok');
    await settle();
    http.expectOne(groupsPath).flush({ data: { results: [ritzCarlton], totalCount: 1 } });
  });
});
