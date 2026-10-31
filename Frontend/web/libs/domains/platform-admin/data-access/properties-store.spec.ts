import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Property, PropertyDraft } from '@harmony/platform-admin/domain';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import {
  PROPERTIES_PAGE_SIZE,
  PropertiesStore,
  createPropertyPath,
  propertiesPath,
  updatePropertyPath,
} from './properties-store';

const ROOMS = 120;
const AFTER_CREATE = 2;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

const amman: Property = {
  id: 'p1',
  tenantId: 't1',
  code: 'AMM',
  externalIdentifier: null,
  nameEn: 'Ritz Amman',
  nameAr: 'ريتز عمّان',
  propertyTypeCode: 'HOTEL',
  cityId: 'c1',
  countryCode: 'JO',
  timeZoneId: 'Asia/Amman',
  addressLine: '1 Rainbow Street',
  postalCode: null,
  defaultCurrency: 'JOD',
  defaultLanguageCode: 'en',
  totalRooms: ROOMS,
  legalEnvironmentCode: 'JO-Legal',
  taxEnvironmentCode: 'JO-Tax',
  accountingEnvironmentCode: 'JO-Accounting',
  accommodationEnvironmentCode: 'JO-Accommodation',
  opensOn: null,
  closesOn: null,
  isActive: true,
  licensedRooms: null,
  roomsAboveLicence: false,
  tenantCode: null,
  moduleCodes: [],
  subscriptions: [],
  contacts: [],
};
const draft: PropertyDraft = {
  tenantId: 't1',
  code: 'AQB',
  nameEn: 'Ritz Aqaba',
  nameAr: 'ريتز العقبة',
  propertyTypeCode: 'HOTEL',
  cityId: 'c2',
  addressLine: 'South Beach',
  postalCode: '',
  defaultCurrency: 'JOD',
  defaultLanguageCode: 'en',
  totalRooms: ROOMS,
  legalEnvironmentCode: 'JO-Legal',
  taxEnvironmentCode: 'JO-Tax',
  accountingEnvironmentCode: 'JO-Accounting',
  accommodationEnvironmentCode: 'JO-Accommodation',
  externalIdentifier: '',
  startsOn: '2026-10-24',
  lines: [{ moduleCode: 'FO', rooms: null, terminals: null, renewsAutomatically: true }],
};

describe('PropertiesStore', () => {
  let store: PropertiesStore;
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
    store = TestBed.inject(PropertiesStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('asks for every property first; narrowing to a tenant starts at page one', async () => {
    await settle();
    const request = http.expectOne(propertiesPath);
    expect(request.request.body).toEqual({
      pageNumber: 1,
      pageSize: PROPERTIES_PAGE_SIZE,
      tenantId: null,
      searchText: null,
      isActive: null,
      countryCode: null,
      propertyTypeCode: null,
      state: null,
      aboveLicenceOnly: false,
    });
    request.flush({ data: { results: [amman], totalCount: 1 } });
    await settle();
    expect(store.rows()).toEqual([amman]);
    store.ofTenant('t1');
    await settle();
    expect(http.expectOne(propertiesPath).request.body).toMatchObject({
      tenantId: 't1',
      pageNumber: 1,
    });
  });

  it('create sends the draft with nulls for the blanks and the key; update sends the id and the changed part', async () => {
    await settle();
    http.expectOne(propertiesPath).flush({ data: { results: [amman], totalCount: 1 } });
    const intent = newIntentKey();
    const created = store.create(draft, intent);
    const create = http.expectOne(createPropertyPath);
    expect(create.request.body).toMatchObject({
      code: 'AQB',
      postalCode: null,
      externalIdentifier: null,
      lines: [{ moduleCode: 'FO', renewsAutomatically: true }],
    });
    expect(create.request.headers.get('Idempotency-Key')).toBe(intent.value);
    create.flush({ data: 'p2' });
    expect((await created).kind).toBe('ok');
    await settle();
    http.expectOne(propertiesPath).flush({ data: { results: [amman], totalCount: AFTER_CREATE } });

    const updated = store.update(
      amman,
      {
        nameEn: 'Ritz Amman Downtown',
        nameAr: amman.nameAr,
        addressLine: amman.addressLine,
        postalCode: '',
        totalRooms: ROOMS,
        opensOn: null,
        closesOn: null,
      },
      newIntentKey(),
    );
    const update = http.expectOne(updatePropertyPath);
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toMatchObject({
      id: 'p1',
      nameEn: 'Ritz Amman Downtown',
      postalCode: null,
    });
    update.flush({ data: 1 });
    expect((await updated).kind).toBe('ok');
    await settle();
    http.expectOne(propertiesPath).flush({ data: { results: [amman], totalCount: AFTER_CREATE } });
  });
});
