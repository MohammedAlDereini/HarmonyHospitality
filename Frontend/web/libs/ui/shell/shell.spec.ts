import { ChangeDetectionStrategy, Component, TemplateRef, signal, viewChild } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { AccessStore } from '@harmony/platform/access';
import { AppUpdate } from '@harmony/platform/app-update';
import { CurrentProperty, MyProperties, PropertySummary } from '@harmony/platform/context';
import { FailureNotice } from '@harmony/platform/errors';
import { SessionState, SessionStore, SessionUser } from '@harmony/platform/session';
import { NavItem, NavPanel } from './nav-panel';
import { PageActions } from './page-actions';
import { DockLink, SHELL_PLACES, ShellPlaces } from './places';
import { Shell } from './shell';

/** Stands for a page that offers one button to the context bar. */
@Component({
  selector: 'hr-offer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ng-template #offer><button type="button" class="pbtn">Grid</button></ng-template>`,
})
class Offer {
  readonly template = viewChild.required<TemplateRef<unknown>>('offer');
}

const stale = signal(false);
const newer = signal(false);
const reloadApp = vi.fn();

const ahmad: SessionUser = {
  sub: 'u1',
  name: 'Ahmad Saleh',
  tenant: 't1',
  roles: [],
  amr: ['pwd'],
  logoutUrl: '/bff/logout?sid=1',
};
const grand: PropertySummary = {
  id: 'p-grd',
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
  const items = signal<readonly NavItem[]>([]);
  const dock = signal<readonly DockLink[]>([]);
  const places: ShellPlaces = { scope: 'property', items, dock };

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
    stale.set(false);
    newer.set(false);
    reloadApp.mockClear();
    properties.set([]);
    code.set(null);
    items.set([]);
    dock.set([]);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SessionStore, useValue: session },
        { provide: SHELL_PLACES, useValue: places },
        { provide: AppUpdate, useValue: { stale, newer, reload: reloadApp } },
        {
          provide: AccessStore,
          useValue: { ready: () => Promise.resolve(), can: () => false },
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

  it("the dock and the nav panel show the app's places as they are offered", async () => {
    expect(page.querySelectorAll('.dock a')).toHaveLength(1);
    dock.set([
      { label: 'Users', link: ['/p', 'RYD', 'admin'], icon: 'guest', exact: true },
      { label: 'Roles', link: ['/p', 'RYD', 'admin', 'roles'], icon: 'tape' },
    ]);
    items.set([
      { label: 'Users', link: ['/p', 'RYD', 'admin'], icon: 'guest', group: 'Administration' },
      {
        label: 'Roles',
        link: ['/p', 'RYD', 'admin', 'roles'],
        icon: 'tape',
        group: 'Administration',
      },
    ]);
    await render();
    const links = [...page.querySelectorAll('.dock a')].map((a) => a.getAttribute('href'));
    expect(links).toEqual(['/', '/p/RYD/admin', '/p/RYD/admin/roles']);
    expect(page.querySelector('.dock a[aria-label="Roles"]')).not.toBeNull();
    expect(text('hr-nav-panel')).toContain('Administration');
    expect(text('hr-nav-panel')).toContain('Roles');
  });

  it('a gone session: the strip; Sign in again opens the window; when refused, it asks before a full-page sign-in', async () => {
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
    expect(text('.dlg-h')).toBe('Sign in on this page?');
    click('.dlg-a .btn:not(.solid)');
    await render();
    expect(session.signedInTo).toEqual([]);
    expect(dialog?.hasAttribute('open')).toBe(false);
    click('.strip-session button');
    await render();
    click('.dlg-a .solid');
    await render();
    expect(session.signedInTo).toEqual(['/']);
  });

  it('Ctrl K is the command palette: the places panel is asked to open and the browser keeps its own Ctrl K', () => {
    const panel = fixture.debugElement.query(By.directive(NavPanel)).componentInstance as NavPanel;
    const open = vi.spyOn(panel, 'open');
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

  it('a build below the floor: an alert that asks for a reload; a merely newer release: a quiet offer', async () => {
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

  it('the skip link moves focus to the main landmark', () => {
    click('a.skip');
    expect(document.activeElement).toBe(page.querySelector('main'));
  });

  it("the context bar renders the page's actions next to the breadcrumb, and empties when the page releases them", async () => {
    expect(page.querySelector('.pacts .pbtn')).toBeNull();
    const offer = TestBed.createComponent(Offer);
    offer.detectChanges();
    const actions = TestBed.inject(PageActions);
    actions.take(offer.componentInstance.template());
    await render();
    expect(text('.cbar .pacts .pbtn')).toBe('Grid');
    actions.release(offer.componentInstance.template());
    await render();
    expect(page.querySelector('.pacts .pbtn')).toBeNull();
  });
});
