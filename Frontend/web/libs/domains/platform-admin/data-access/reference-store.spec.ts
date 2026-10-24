import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import {
  REFERENCE_PAGE_SIZE,
  ReferenceStore,
  createPath,
  listPath,
  rowPath,
  updatePath,
} from './reference-store';

const MINOR_UNITS = 3;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));
const jod = {
  code: 'JOD',
  nameEn: 'Jordanian dinar',
  nameAr: 'دينار أردني',
  minorUnits: MINOR_UNITS,
  isActive: true,
};

describe('ReferenceStore', () => {
  let store: ReferenceStore;
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
    store = TestBed.inject(ReferenceStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('a table reads nothing until asked, then one page of 100; the same table is handed back; a scope narrows it', async () => {
    const currencies = store.table('Currency');
    expect(store.table('Currency')).toBe(currencies);
    await settle();
    http.expectNone(listPath('Currency'));
    currencies.load();
    await settle();
    const request = http.expectOne(listPath('Currency'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      pageNumber: 1,
      pageSize: REFERENCE_PAGE_SIZE,
      searchText: null,
    });
    request.flush({ data: { results: [jod], totalCount: 1 } });
    await settle();
    expect(currencies.rows()).toEqual([jod]);
    const values = store.table('LookupValue');
    values.load({ lookupCategoryId: 'c1' });
    await settle();
    expect(http.expectOne(listPath('LookupValue')).request.body).toMatchObject({
      lookupCategoryId: 'c1',
    });
  });

  it('create, update and remove carry the key and read that table again on success', async () => {
    const currencies = store.table('Currency');
    currencies.load();
    await settle();
    http.expectOne(listPath('Currency')).flush({ data: { results: [], totalCount: 0 } });
    const intent = newIntentKey();
    const created = store.create(
      'Currency',
      { code: 'USD', nameEn: 'US dollar', nameAr: 'دولار' },
      intent,
    );
    const create = http.expectOne(createPath('Currency'));
    expect(create.request.method).toBe('POST');
    expect(create.request.headers.get('Idempotency-Key')).toBe(intent.value);
    create.flush({ data: 'USD' });
    expect((await created).kind).toBe('ok');
    await settle();
    http.expectOne(listPath('Currency')).flush({ data: { results: [jod], totalCount: 1 } });

    const updated = store.update('Currency', { ...jod, nameEn: 'Dinar' }, newIntentKey());
    const update = http.expectOne(updatePath('Currency'));
    expect(update.request.method).toBe('PUT');
    update.flush({ data: 1 });
    expect((await updated).kind).toBe('ok');
    await settle();
    http.expectOne(listPath('Currency')).flush({ data: { results: [jod], totalCount: 1 } });

    const removed = store.remove('LookupCategory', 'c1', newIntentKey());
    const remove = http.expectOne(rowPath('LookupCategory', 'c1'));
    expect(remove.request.method).toBe('DELETE');
    remove.flush({ data: 1 });
    expect((await removed).kind).toBe('ok');
    await settle();
    http.expectNone(listPath('LookupCategory'));
  });
});
