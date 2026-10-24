import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AccessStore } from '@harmony/platform/access';
import { CurrentProperty, MyProperties, PropertySummary } from '@harmony/platform/context';
import { FailureNotice } from '@harmony/platform/errors';
import { SessionState, SessionStore, SessionUser } from '@harmony/platform/session';
import { Shell } from './shell';

const ahmad: SessionUser = {
  sub: 'u1',
  name: 'Ahmad Saleh',
  tenant: 't1',
  roles: [],
  amr: ['pwd'],
  logoutUrl: '/bff/logout?sid=1',
};
const grand: PropertySummary = {
  code: 'GRD',
  nameEn: 'Grand Riyadh',
  nameAr: 'غراند الرياض',
  timeZoneId: 'Asia/Riyadh',
  currency: 'SAR',
};

class FakeSession {
  readonly state = signal<SessionState>('signed-in');
  readonly user = signal<SessionUser | null>(ahmad);
  readonly resumed = signal(0);
  windowRefused = false;
  opened = 0;
  refreshed = 0;
  signedOut = 0;
  signedInTo: string[] = [];
  ready(): Promise<SessionState> {
    return Promise.resolve(this.state());
  }
  refresh(): Promise<void> {
    this.refreshed += 1;
    return Promise.resolve();
  }
  signIn(returnUrl: string): void {
    this.signedInTo.push(returnUrl);
  }
  signInAgain(): boolean {
    this.opened += 1;
    return !this.windowRefused;
  }
  signOut(): void {
    this.signedOut += 1;
  }
}

// Runs of whitespace (spaces, newlines) collapse to one space: matches "a \n b" (→ "a b"); rejects "ab" (nothing to do).
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

describe('Shell', () => {
  let session: FakeSession;
  let notice: FailureNotice;
  let fixture: ComponentFixture<Shell>;
  let page: HTMLElement;
  const properties = signal<readonly PropertySummary[]>([]);
  const code = signal<string | null>(null);
  let canViewUsers = false;

  const text = (selector: string): string =>
    page.querySelector(selector)?.textContent.replace(whitespace, ' ').trim() ?? '';
  const click = (selector: string): void => {
    page.querySelector<HTMLElement>(selector)?.click();
  };
  const render = async (): Promise<void> => {
    fixture.detectChanges();
    await fixture.whenStable();
  };

  beforeEach(async () => {
    session = new FakeSession();
    properties.set([]);
    code.set(null);
    canViewUsers = false;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SessionStore, useValue: session },
        {
          provide: AccessStore,
          useValue: { ready: () => Promise.resolve(), can: () => canViewUsers },
        },
        {
          provide: MyProperties,
          useValue: { list: properties, status: signal('ready'), ready: () => Promise.resolve() },
        },
        { provide: CurrentProperty, useValue: { code } },
      ],
    });
    notice = TestBed.inject(FailureNotice);
    fixture = TestBed.createComponent(Shell);
    page = fixture.nativeElement as HTMLElement;
    await render();
  });

  it('the bar carries the person as initials and a readable name; the account menu signs out', () => {
    expect(text('.gbar .av')).toBe('AS');
    expect(text('.gbar bdi.vh')).toBe('Ahmad Saleh');
    expect(text('#me-pop .pop-who b')).toBe('Ahmad Saleh');
    expect(page.querySelector('.strip')).toBeNull();
    click('#me-pop .pop-i');
    expect(session.signedOut).toBe(1);
  });

  it('outside a property the scope button invites a choice; inside, it names the hotel and ticks it in the menu', async () => {
    expect(text('.gprop b')).toBe('Choose a property');
    properties.set([grand]);
    code.set('GRD');
    await render();
    expect(text('.gprop b')).toBe('Grand Riyadh');
    expect(page.querySelector('#prop-pop a[aria-current="true"]')?.getAttribute('href')).toBe(
      '/p/GRD',
    );
    expect(text('#prop-pop .pcode')).toBe('GRD');
  });

  it('the dock shows Users only inside a property and only with the permission', async () => {
    expect(page.querySelectorAll('.dock a')).toHaveLength(1);
    code.set('GRD');
    await render();
    expect(page.querySelectorAll('.dock a')).toHaveLength(1);
    canViewUsers = true;
    code.set('RYD');
    await render();
    const links = [...page.querySelectorAll('.dock a')].map((a) => a.getAttribute('href'));
    expect(links).toEqual(['/', '/p/RYD/admin']);
    expect(text('hr-nav-panel')).toContain('Administration');
  });

  it('a gone session: the strip; Sign in again opens the window; when refused, it asks before a full-page sign-in', async () => {
    session.state.set('expired');
    await render();
    expect(text('.strip-session')).toContain('Your session has ended');
    click('.strip-session button');
    expect(session.opened).toBe(1);
    expect(session.signedInTo).toEqual([]);
    session.windowRefused = true;
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    click('.strip-session button');
    expect(session.signedInTo).toEqual([]);
    confirm.mockReturnValue(true);
    click('.strip-session button');
    expect(session.signedInTo).toEqual(['/']);
    confirm.mockRestore();
  });

  it('the BFF not responding: the strip and Try again', async () => {
    session.state.set('unavailable');
    await render();
    expect(text('.strip-down')).toContain('not responding');
    click('.strip-down button');
    expect(session.refreshed).toBe(1);
  });

  it('an unexpected failure: an alert with the reference, Reload and Dismiss', async () => {
    const reload = vi.spyOn(notice, 'reload').mockImplementation(() => undefined);
    notice.show({ reference: '7f3a2c1d', message: 'Something went wrong.' });
    await render();
    expect(text('[role="alert"]')).toContain('Something went wrong. Reference 7f3a2c1d');
    click('[role="alert"] .btn:not(.quiet)');
    expect(reload).toHaveBeenCalledOnce();
    click('[role="alert"] .quiet');
    await render();
    expect(page.querySelector('[role="alert"]')).toBeNull();
    reload.mockRestore();
  });

  it('the skip link moves focus to the main landmark', () => {
    click('a.skip');
    expect(document.activeElement).toBe(page.querySelector('main'));
  });
});
