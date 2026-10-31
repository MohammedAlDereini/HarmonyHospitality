import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { AccessStore } from '@harmony/platform/access';
import { AppUpdate } from '@harmony/platform/app-update';
import { RUNTIME_CONFIG } from '@harmony/platform/config';
import { FailureNotice } from '@harmony/platform/errors';
import { FlagsStore } from '@harmony/platform/flags';
import { SessionState, SessionStore, SessionUser } from '@harmony/platform/session';
import { NavItem, NavPanel } from '@harmony/ui/shell';
import {
  AttentionItem,
  CONSOLE_ATTENTION,
  CONSOLE_PLACES,
  ConsoleLink,
  ConsolePlaces,
  ConsoleSection,
} from './console-places';
import { ConsoleShell } from './console-shell';

/** Stands for a page with a heading, so the title and the focus walk have something to land on. */
@Component({
  selector: 'hr-overview-stub',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h1>Overview</h1>`,
})
class OverviewStub {}

const stale = signal(false);
const newer = signal(false);
const reloadApp = vi.fn();
const ring = signal<string | null>(null);

const dana: SessionUser = {
  sub: 'u1',
  name: 'Dana Saleh',
  tenant: 't1',
  roles: [],
  amr: ['pwd'],
  logoutUrl: '/bff/logout?sid=1',
};

class FakeSession {
  readonly state = signal<SessionState>('signed-in');
  readonly user = signal<SessionUser | null>(dana);
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

/** A fake window.matchMedia: the one query the shell asks, answered as told, with the listeners kept to count. */
class FakeMedia {
  matches: boolean;
  readonly listeners: ((event: MediaQueryListEvent) => void)[] = [];
  constructor(matches: boolean) {
    this.matches = matches;
  }
  addEventListener(_type: string, listener: (event: MediaQueryListEvent) => void): void {
    this.listeners.push(listener);
  }
  removeEventListener(_type: string, listener: (event: MediaQueryListEvent) => void): void {
    this.listeners.splice(this.listeners.indexOf(listener), 1);
  }
  resize(matches: boolean): void {
    this.matches = matches;
    this.listeners.forEach((listener) => {
      listener({ matches } as MediaQueryListEvent);
    });
  }
}

const home: ConsoleLink = { label: 'Home', link: ['/'], icon: 'house', exact: true };
const tenants: ConsoleLink = { label: 'Tenants', link: ['/tenants'], icon: 'tenants' };
const health: ConsoleLink = { label: 'Health', link: ['/health'], icon: 'health' };
const settings: ConsoleLink = { label: 'Settings', link: ['/settings'], icon: 'settings' };
const waiting: readonly AttentionItem[] = [
  {
    id: 'a1',
    kind: 'Suspended',
    text: 'Pearl Hotels is suspended',
    link: ['/tenants'],
    tone: 'stop',
  },
  { id: 'a2', kind: 'Approval', text: 'One approval is pending', link: ['/people'], tone: 'warn' },
];

// Runs of whitespace (spaces, newlines) collapse to one space: matches "a \n b" (→ "a b"); rejects "ab" (nothing to do).
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;
/** How many times the palette has been asked to open after Ctrl K, then the search button, then the menu button. */
const OPENED_BY_SEARCH = 2;
const OPENED_BY_MENU = 3;

describe('ConsoleShell', () => {
  let session: FakeSession;
  let notice: FailureNotice;
  let fixture: ComponentFixture<ConsoleShell>;
  let page: HTMLElement;
  const sections = signal<readonly ConsoleSection[]>([]);
  const bottom = signal<readonly ConsoleLink[]>([]);
  const phone = signal<readonly ConsoleLink[]>([]);
  const items = signal<readonly NavItem[]>([]);
  const attention = signal<readonly AttentionItem[]>([]);
  const places: ConsolePlaces = { sections, bottom, phone, items };

  const text = (selector: string): string =>
    page.querySelector(selector)?.textContent.replace(whitespace, ' ').trim() ?? '';
  const click = (selector: string): void => {
    page.querySelector<HTMLElement>(selector)?.click();
  };
  const hrefs = (selector: string): (string | null)[] =>
    [...page.querySelectorAll(selector)].map((a) => a.getAttribute('href'));
  const render = async (): Promise<void> => {
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const mount = async (): Promise<void> => {
    fixture = TestBed.createComponent(ConsoleShell);
    page = fixture.nativeElement as HTMLElement;
    await render();
  };

  beforeEach(() => {
    session = new FakeSession();
    stale.set(false);
    newer.set(false);
    ring.set(null);
    reloadApp.mockClear();
    sections.set([
      { heading: 'Platform', items: [home, tenants] },
      { heading: 'Operations', items: [health] },
    ]);
    bottom.set([settings]);
    phone.set([home, tenants, health]);
    items.set([{ ...home, group: 'Platform' }]);
    attention.set([]);
    localStorage.removeItem('harmony.console.nav');
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '', component: OverviewStub, title: 'Overview' }]),
        { provide: SessionStore, useValue: session },
        { provide: CONSOLE_PLACES, useValue: places },
        { provide: CONSOLE_ATTENTION, useValue: attention },
        { provide: AppUpdate, useValue: { stale, newer, reload: reloadApp } },
        { provide: FlagsStore, useValue: { ring } },
        {
          provide: RUNTIME_CONFIG,
          useValue: { environment: 'Staging', release: '2026.10.29.1', minimumClientVersion: '' },
        },
        {
          provide: AccessStore,
          useValue: { ready: () => Promise.resolve(), can: () => false },
        },
      ],
    });
    notice = TestBed.inject(FailureNotice);
  });

  it('the bar: the lockup, the person as initials and a readable name, the environment and the ring once known', async () => {
    await mount();
    expect(page.querySelector('hr-console-lockup')).not.toBeNull();
    expect(text('.tb-me .av')).toBe('DS');
    expect(text('.tb-me .nm')).toBe('Dana Saleh');
    expect(text('.tb-env')).toBe('Staging');
    ring.set('Everyone');
    await render();
    expect(text('.tb-env')).toBe('Staging · Everyone ring');
    expect(text('#me-pop .pop-who b')).toBe('Dana Saleh');
    click('#me-pop .pop-sec:last-child .pop-i');
    expect(session.signedOut).toBe(1);
  });

  it("the rail shows the app's sections and the foot; the phone bar its own places; the palette the flat list", async () => {
    await mount();
    expect([...page.querySelectorAll('.side .sn-g')].map((g) => g.textContent.trim())).toEqual([
      'Platform',
      'Operations',
    ]);
    expect(hrefs('.side a.sn-i')).toEqual(['/', '/tenants', '/health', '/settings']);
    expect(hrefs('.bottombar a')).toEqual(['/', '/tenants', '/health']);
    expect(text('hr-nav-panel')).toContain('Platform');
    sections.set([{ heading: 'Platform', items: [home] }]);
    bottom.set([]);
    await render();
    expect(hrefs('.side a.sn-i')).toEqual(['/']);
  });

  it('the fold button folds the rail to icons, names each place by its title, and keeps the choice on this device', async () => {
    await mount();
    const side = page.querySelector('.side');
    const fold = page.querySelector<HTMLButtonElement>('.sn-fold');
    expect(side?.classList.contains('folded')).toBe(false);
    expect(fold?.getAttribute('aria-expanded')).toBe('true');
    expect(fold?.getAttribute('aria-label')).toBe('Collapse');
    expect(page.querySelector('.side a.sn-i')?.getAttribute('title')).toBeNull();
    click('.sn-fold');
    await render();
    expect(side?.classList.contains('folded')).toBe(true);
    expect(fold?.getAttribute('aria-expanded')).toBe('false');
    expect(fold?.getAttribute('aria-label')).toBe('Expand');
    expect(page.querySelector('.side a.sn-i')?.getAttribute('title')).toBe('Home');
    expect(localStorage.getItem('harmony.console.nav')).toBe('collapsed');
    click('.sn-fold');
    await render();
    expect(side?.classList.contains('folded')).toBe(false);
    expect(localStorage.getItem('harmony.console.nav')).toBeNull();
  });

  it('a tablet window folds the rail on its own, and opens it again when the window grows; the listener goes with the shell', async () => {
    const media = new FakeMedia(true);
    const view = window as unknown as Record<string, unknown>;
    view['matchMedia'] = (): FakeMedia => media;
    try {
      await mount();
      const side = page.querySelector('.side');
      expect(side?.classList.contains('folded')).toBe(true);
      expect(media.listeners).toHaveLength(1);
      media.resize(false);
      await render();
      expect(side?.classList.contains('folded')).toBe(false);
      fixture.destroy();
      expect(media.listeners).toHaveLength(0);
    } finally {
      delete view['matchMedia'];
    }
  });

  it('the bell counts what needs a platform person and lists it; silent when nothing waits', async () => {
    await mount();
    expect(page.querySelector('.tb-ic .badge')).toBeNull();
    expect(text('#needs-pop .pop-note')).toBe('Nothing needs you right now.');
    attention.set(waiting);
    await render();
    expect(text('.tb-ic .badge')).toBe('2');
    expect(hrefs('#needs-pop a.needs-i')).toEqual(['/tenants', '/people']);
    expect(page.querySelector('#needs-pop .chip')?.getAttribute('data-s')).toBe('stop');
    expect(text('#needs-pop .needs-i .needs-t')).toBe('Pearl Hotels is suspended');
  });

  it('the help menu points at platform health and the API documentation and names the release', async () => {
    await mount();
    expect(hrefs('#help-pop a.pop-i')).toEqual(['/health', '/docs']);
    expect(text('#help-pop .pop-note')).toBe('Console release 2026.10.29.1');
  });

  it("the phone bar's title is the page's title once a route has one", async () => {
    await mount();
    await TestBed.inject(Router).navigateByUrl('/');
    await render();
    expect(text('.tb-title')).toBe('Overview');
  });

  it('a gone session: the strip; Sign in again opens the window; when refused, it asks before a full-page sign-in', async () => {
    await mount();
    session.state.set('expired');
    await render();
    expect(text('.strip-session')).toContain('Your session has ended');
    click('.strip-session button');
    expect(session.opened).toBe(1);
    expect(session.signedInTo).toEqual([]);
    session.windowRefused = true;
    click('.strip-session button');
    await render();
    const dialog = page.querySelector<HTMLDialogElement>('dialog.dlg');
    expect(dialog?.hasAttribute('open')).toBe(true);
    click('.dlg-a .btn:not(.solid)');
    await render();
    expect(session.signedInTo).toEqual([]);
    click('.strip-session button');
    await render();
    click('.dlg-a .solid');
    await render();
    expect(session.signedInTo).toEqual(['/']);
  });

  it('Ctrl K and the search button open the places panel; the plain key is left to the browser', async () => {
    await mount();
    const panel = fixture.debugElement.query(By.directive(NavPanel)).componentInstance as NavPanel;
    const open = vi.spyOn(panel, 'open').mockImplementation(() => undefined);
    const plainK = new KeyboardEvent('keydown', { key: 'k', bubbles: true, cancelable: true });
    document.dispatchEvent(plainK);
    expect(open).not.toHaveBeenCalled();
    const ctrlK = new KeyboardEvent('keydown', {
      key: 'k',
      ctrlKey: true,
      bubbles: true,
      cancelable: true,
    });
    document.dispatchEvent(ctrlK);
    expect(open).toHaveBeenCalledOnce();
    expect(ctrlK.defaultPrevented).toBe(true);
    click('.tb-search');
    expect(open).toHaveBeenCalledTimes(OPENED_BY_SEARCH);
    click('.tb-menu');
    expect(open).toHaveBeenCalledTimes(OPENED_BY_MENU);
  });

  it('the BFF not responding: the strip and Try again', async () => {
    await mount();
    session.state.set('unavailable');
    await render();
    expect(text('.strip-down')).toContain('not responding');
    click('.strip-down button');
    expect(session.refreshed).toBe(1);
  });

  it('an unexpected failure: an alert with the reference, Reload and Dismiss', async () => {
    await mount();
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

  it('a build below the floor: an alert that asks for a reload; a merely newer release: a quiet offer', async () => {
    await mount();
    expect(page.querySelector('.strip-update, .strip-update-soft')).toBeNull();
    newer.set(true);
    await render();
    expect(page.querySelector('.strip-update-soft[role="status"]')).not.toBeNull();
    stale.set(true);
    await render();
    expect(page.querySelector('.strip-update-soft')).toBeNull();
    click('.strip-update[role="alert"] .btn');
    expect(reloadApp).toHaveBeenCalledOnce();
  });

  it('the skip link moves focus to the main landmark', async () => {
    await mount();
    click('a.skip');
    expect(document.activeElement).toBe(page.querySelector('main'));
  });
});
