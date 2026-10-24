import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AccessStore } from '@harmony/platform/access';
import { FailureNotice } from '@harmony/platform/errors';
import { SessionState, SessionStore, SessionUser } from '@harmony/platform/session';
import { Shell } from './shell';

const ahmad: SessionUser = {
  sub: 'u1',
  name: 'Ahmad',
  tenant: 't1',
  roles: [],
  amr: ['pwd'],
  logoutUrl: '/bff/logout?sid=1',
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
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SessionStore, useValue: session },
        { provide: AccessStore, useValue: { ready: () => Promise.resolve() } },
      ],
    });
    notice = TestBed.inject(FailureNotice);
    fixture = TestBed.createComponent(Shell);
    page = fixture.nativeElement as HTMLElement;
    await render();
  });

  it('the bar names the person and offers sign-out; no strip while all is well', () => {
    expect(text('.gbar bdi')).toBe('Ahmad');
    expect(page.querySelector('.strip')).toBeNull();
    click('.gbtn');
    expect(session.signedOut).toBe(1);
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
    click('[role="alert"] .btn:not(.btn-quiet)');
    expect(reload).toHaveBeenCalledOnce();
    click('[role="alert"] .btn-quiet');
    await render();
    expect(page.querySelector('[role="alert"]')).toBeNull();
    reload.mockRestore();
  });

  it('the skip link moves focus to the main landmark', () => {
    click('a.skip');
    expect(document.activeElement).toBe(page.querySelector('main'));
  });
});
