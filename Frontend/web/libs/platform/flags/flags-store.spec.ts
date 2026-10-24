import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { API_RETRY_DELAYS, provideHarmonyHttp } from '@harmony/platform/http';
import { SessionStore, SessionUser } from '@harmony/platform/session';
import { FlagsStore, flagsPath } from './flags-store';
import { IfFlag } from './if-flag.directive';

const UNAUTHORIZED = 401;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

const noor: SessionUser = {
  sub: 'u1',
  name: 'Noor',
  tenant: 't0',
  roles: [],
  amr: ['pwd'],
  logoutUrl: '/bff/logout?sid=1',
};

@Component({
  selector: 'hr-host',
  imports: [IfFlag],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p *hrIfFlag="'night-audit'" class="flagged">Night audit</p>`,
})
class Host {}

describe('FlagsStore', () => {
  const user = signal<SessionUser | null>(null);
  let store: FlagsStore;
  let http: HttpTestingController;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };

  beforeEach(() => {
    user.set(null);
    TestBed.configureTestingModule({
      providers: [
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
        { provide: SessionStore, useValue: { user } },
      ],
    });
    store = TestBed.inject(FlagsStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('asks nothing while nobody is signed in; then reads the flags once, with the CSRF header; unknown flags are off', async () => {
    await settle();
    http.expectNone(flagsPath);
    expect(store.on('night-audit')()).toBe(false);
    user.set(noor);
    await settle();
    const request = http.expectOne(flagsPath);
    expect(request.request.headers.get('x-csrf')).toBe('1');
    request.flush({
      ring: 'early',
      release: '1.2.0',
      minimumClientVersion: '0',
      flags: { 'night-audit': true, 'rate-plans': false },
    });
    await settle();
    expect(store.ring()).toBe('early');
    expect(store.on('night-audit')()).toBe(true);
    expect(store.on('rate-plans')()).toBe(false);
    expect(store.on('never-heard-of')()).toBe(false);
  });

  it('a door that does not answer leaves every flag off', async () => {
    user.set(noor);
    await settle();
    http.expectOne(flagsPath).flush(null, { status: UNAUTHORIZED, statusText: 'Unauthorized' });
    await settle();
    expect(store.known()).toBe(false);
    expect(store.on('night-audit')()).toBe(false);
  });

  it('*hrIfFlag shows its template while the flag is on and removes it when the flag goes off', async () => {
    user.set(noor);
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await settle();
    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('.flagged')).toBeNull();
    http.expectOne(flagsPath).flush({
      ring: 'all',
      release: '1',
      minimumClientVersion: '0',
      flags: { 'night-audit': true },
    });
    await settle();
    fixture.detectChanges();
    expect(page.querySelector('.flagged')?.textContent).toBe('Night audit');
    store.reload();
    await settle();
    http.expectOne(flagsPath).flush({
      ring: 'all',
      release: '1',
      minimumClientVersion: '0',
      flags: { 'night-audit': false },
    });
    await settle();
    fixture.detectChanges();
    expect(page.querySelector('.flagged')).toBeNull();
  });
});
