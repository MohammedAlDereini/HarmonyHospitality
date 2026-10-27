import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { EffectiveSetting, SettingPlace } from '@harmony/administration/domain';
import { CurrentProperty } from '@harmony/platform/context';
import { API_RETRY_DELAYS, newIntentKey, provideHarmonyHttp } from '@harmony/platform/http';
import { SettingsStore, clearValuePath, effectivePath, setValuePath } from './settings-store';

const ACCEPTED = 202;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

const checkIn: EffectiveSetting = {
  key: 'frontoffice.checkInTime',
  ownerService: 'FrontOffice',
  dataType: 'time',
  outcome: 'Value',
  value: '15:00',
  source: 'SystemDefault',
  sourceCode: null,
  trace: [],
};
const hotel: SettingPlace = { scope: 'Property', scopeId: 'p-amman', code: 'AMMAN', value: null };
const customer: SettingPlace = { scope: 'Tenant', scopeId: null, code: null, value: '14:00' };

describe('SettingsStore', () => {
  let store: SettingsStore;
  let http: HttpTestingController;
  const code = signal<string | null>(null);

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };

  beforeEach(() => {
    code.set(null);
    TestBed.configureTestingModule({
      providers: [
        SettingsStore,
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
        { provide: CurrentProperty, useValue: { code } },
      ],
    });
    store = TestBed.inject(SettingsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('reads nothing outside a hotel; inside one, every setting with one GET; another hotel reads again', async () => {
    await settle();
    http.expectNone(effectivePath);
    code.set('AMMAN');
    await settle();
    const first = http.expectOne(effectivePath);
    expect(first.request.method).toBe('GET');
    first.flush({ data: [checkIn] });
    await settle();
    expect(store.settings()).toEqual([checkIn]);
    code.set('PETRA');
    await settle();
    http.expectOne(effectivePath).flush({ data: [] });
    await settle();
    expect(store.settings()).toEqual([]);
  });

  it('a set carries the place and the key, and reads again; a clear names the place; a refusal reads nothing', async () => {
    code.set('AMMAN');
    await settle();
    http.expectOne(effectivePath).flush({ data: [checkIn] });
    const intent = newIntentKey();
    const done = store.set(checkIn.key, hotel, '16:00', intent);
    const put = http.expectOne(setValuePath);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({
      key: checkIn.key,
      scope: 'Property',
      scopeId: 'p-amman',
      value: '16:00',
    });
    expect(put.request.headers.get('Idempotency-Key')).toBe(intent.value);
    put.flush({ data: 'v1' });
    expect((await done).kind).toBe('ok');
    await settle();
    http.expectOne(effectivePath).flush({ data: [checkIn] });

    const refused = store.clear(checkIn.key, customer, newIntentKey());
    const clear = http.expectOne(clearValuePath);
    expect(clear.request.body).toEqual({ key: checkIn.key, scope: 'Tenant', scopeId: null });
    clear.flush({ errors: [{ code: '00116' }] }, { status: ACCEPTED, statusText: 'Accepted' });
    expect(await refused).toEqual({ kind: 'refused', status: ACCEPTED, codes: ['00116'] });
    await settle();
    http.expectNone(effectivePath);
  });
});
