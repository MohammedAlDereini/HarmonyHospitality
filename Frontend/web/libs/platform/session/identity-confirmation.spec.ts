import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  API_RETRY_DELAYS,
  ApiResult,
  IntentKey,
  newIntentKey,
  provideHarmonyHttp,
} from '@harmony/platform/http';
import {
  CONFIRMATION_REQUIRED,
  IdentityConfirmation,
  confirmIdentityPath,
} from './identity-confirmation';

const OK = 200;
const ACCEPTED = 202;
const SENT_TWICE = 2;
const ok: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
const confirmFirst: ApiResult<unknown> = {
  kind: 'refused',
  status: ACCEPTED,
  codes: [CONFIRMATION_REQUIRED],
};
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

describe('IdentityConfirmation', () => {
  let confirmation: IdentityConfirmation;
  let http: HttpTestingController;
  let keys: string[];

  /** A guarded change that wants a confirmation the first time only. */
  const guarded = (answers: ApiResult<unknown>[]) => (intent: IntentKey) => {
    keys.push(intent.value);
    return Promise.resolve(answers[keys.length - 1] ?? ok);
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
      ],
    });
    confirmation = TestBed.inject(IdentityConfirmation);
    http = TestBed.inject(HttpTestingController);
    keys = [];
  });

  afterEach(() => {
    http.verify();
  });

  it('a change that needs nothing goes once and asks nothing', async () => {
    expect(await confirmation.run(guarded([ok]))).toEqual(ok);
    expect(keys.length).toBe(1);
    expect(confirmation.asking()).toBe(false);
  });

  it('00084: asks; the password goes to Identity with a key; then the change goes again with a new key', async () => {
    const running = confirmation.run(guarded([confirmFirst, ok]));
    await tick();
    expect(confirmation.asking()).toBe(true);

    const key = newIntentKey();
    const confirmed = confirmation.confirm('typed-by-the-person', key);
    const put = http.expectOne(confirmIdentityPath);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ password: 'typed-by-the-person' });
    expect(put.request.headers.get('Idempotency-Key')).toBe(key.value);
    put.flush({ data: 1 });
    expect((await confirmed).kind).toBe('ok');

    expect(await running).toEqual(ok);
    expect(confirmation.asking()).toBe(false);
    expect(keys.length).toBe(SENT_TWICE);
    expect(keys[0]).not.toBe(keys[1]);
  });

  it('a wrong password keeps the question open; staying answers the first refusal and sends nothing more', async () => {
    const running = confirmation.run(guarded([confirmFirst, ok]));
    await tick();
    const wrong = confirmation.confirm('not-it', newIntentKey());
    http
      .expectOne(confirmIdentityPath)
      .flush({ errors: [{ code: '00044' }] }, { status: ACCEPTED, statusText: 'Accepted' });
    expect(await wrong).toEqual({ kind: 'refused', status: ACCEPTED, codes: ['00044'] });
    expect(confirmation.asking()).toBe(true);

    confirmation.dismiss();
    expect(await running).toEqual(confirmFirst);
    expect(keys.length).toBe(1);
  });

  it('a second question while one is open is answered "no" at once', async () => {
    const first = confirmation.ask();
    expect(await confirmation.ask()).toBe(false);
    confirmation.dismiss();
    expect(await first).toBe(false);
  });
});
