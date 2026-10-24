import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import { ASCENDING, DESCENDING, SortOrder } from '@harmony/shared/domain';
import {
  REFERENCE_OPTIONS_SIZE,
  REFERENCE_PAGE_SIZE,
  ReferenceStore,
  createPath,
  listPath,
  rowPath,
  updatePath,
} from './reference-store';

const MINOR_UNITS = 3;
const SECOND_PAGE = 2;
const SEVEN = 7;
const TWO_READS = 2;
const FORBIDDEN = 403;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));
const jod = {
  code: 'JOD',
  nameEn: 'Jordanian dinar',
  nameAr: 'دينار أردني',
  minorUnits: MINOR_UNITS,
  isActive: true,
};
const byCode: SortOrder = { memberName: 'Code', sortOrder: ASCENDING };

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

  it('a table reads nothing until asked, then one page of twenty in its default order; the same table is handed back; a scope narrows it', async () => {
    const currencies = store.table('Currency', [byCode]);
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
      sortOrder: [byCode],
    });
    request.flush({ data: { results: [jod], totalCount: 1 } });
    await settle();
    expect(currencies.rows()).toEqual([jod]);
    expect(currencies.total()).toBe(1);
    const values = store.table('LookupValue');
    values.load({ lookupCategoryId: 'c1' });
    await settle();
    expect(http.expectOne(listPath('LookupValue')).request.body).toMatchObject({
      lookupCategoryId: 'c1',
      sortOrder: [],
    });
  });

  it('a query changes the sort, the filters (trimmed search, flags as flags, blanks dropped) and the page; a new sort or filter goes back to page 1', async () => {
    const countries = store.table('Country', [byCode]);
    countries.query({ page: SECOND_PAGE });
    await settle();
    expect(http.expectOne(listPath('Country')).request.body).toMatchObject({
      pageNumber: SECOND_PAGE,
    });
    countries.query({ sort: { memberName: 'NameEn', sortOrder: DESCENDING } });
    await settle();
    expect(http.expectOne(listPath('Country')).request.body).toMatchObject({
      pageNumber: 1,
      sortOrder: [{ memberName: 'NameEn', sortOrder: DESCENDING }],
    });
    countries.query({ page: SECOND_PAGE });
    await settle();
    http.expectOne(listPath('Country'));
    countries.query({ filters: { searchText: ' jo ', isActive: 'true', countryCode: '' } });
    await settle();
    const filtered = http.expectOne(listPath('Country')).request.body as Record<string, unknown>;
    expect(filtered).toMatchObject({ pageNumber: 1, searchText: 'jo', isActive: true });
    expect('countryCode' in filtered).toBe(false);
    expect(countries.filters()).toEqual({ searchText: ' jo ', isActive: 'true', countryCode: '' });
  });

  it('choices read the whole table for a select, or one scope of it, and are not read twice for the same scope', async () => {
    const subdivisions = store.choices('CountrySubdivision');
    expect(store.choices('CountrySubdivision')).toBe(subdivisions);
    subdivisions.load({ countryCode: 'JO' });
    await settle();
    expect(http.expectOne(listPath('CountrySubdivision')).request.body).toEqual({
      pageNumber: 1,
      pageSize: REFERENCE_OPTIONS_SIZE,
      searchText: null,
      countryCode: 'JO',
    });
    subdivisions.load({ countryCode: 'JO' });
    await settle();
    http.expectNone(listPath('CountrySubdivision'));
    subdivisions.load({ countryCode: 'SA' });
    await settle();
    expect(http.expectOne(listPath('CountrySubdivision')).request.body).toMatchObject({
      countryCode: 'SA',
    });
  });

  it('a count asks for one row and keeps the total; a refused door reads as denied', async () => {
    const currencies = store.count('Currency');
    const modules = store.count('Module');
    await settle();
    expect(currencies.total()).toBeNull();
    http.expectOne(listPath('Currency')).flush({ data: { results: [jod], totalCount: SEVEN } });
    http
      .expectOne(listPath('Module'))
      .flush({ errors: [{ code: '000' }] }, { status: FORBIDDEN, statusText: 'Forbidden' });
    await settle();
    expect(currencies.total()).toBe(SEVEN);
    expect(modules.total()).toBe('denied');
  });

  it('create, update and remove carry the key and read that table, its choices and its count again on success', async () => {
    const currencies = store.table('Currency');
    currencies.load();
    const choices = store.choices('Currency');
    choices.load();
    await settle();
    const first = http.match(listPath('Currency'));
    expect(first).toHaveLength(TWO_READS);
    first.forEach((read) => {
      read.flush({ data: { results: [], totalCount: 0 } });
    });
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
    const again = http.match(listPath('Currency'));
    expect(again).toHaveLength(TWO_READS);
    again.forEach((read) => {
      read.flush({ data: { results: [jod], totalCount: 1 } });
    });

    const updated = store.update('Currency', { ...jod, nameEn: 'Dinar' }, newIntentKey());
    const update = http.expectOne(updatePath('Currency'));
    expect(update.request.method).toBe('PUT');
    update.flush({ data: 1 });
    expect((await updated).kind).toBe('ok');
    await settle();
    const third = http.match(listPath('Currency'));
    expect(third).toHaveLength(TWO_READS);
    third.forEach((read) => {
      read.flush({ data: { results: [jod], totalCount: 1 } });
    });

    const removed = store.remove('LookupCategory', 'c1', newIntentKey());
    const remove = http.expectOne(rowPath('LookupCategory', 'c1'));
    expect(remove.request.method).toBe('DELETE');
    remove.flush({ data: 1 });
    expect((await removed).kind).toBe('ok');
    await settle();
    http.expectNone(listPath('LookupCategory'));
  });
});
