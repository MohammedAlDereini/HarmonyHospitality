import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { NavItem, NavPanel } from './nav-panel';

const places: readonly NavItem[] = [
  { label: 'Home', link: ['/'], icon: 'home', group: 'Workspaces', exact: true },
  { label: 'Users', link: ['/p', 'GRD', 'admin'], icon: 'guest', group: 'Administration' },
];

@Component({
  selector: 'hr-host',
  imports: [NavPanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hr-nav-panel [items]="items()" />`,
})
class Host {
  readonly items = signal(places);
}

describe('hr-nav-panel', () => {
  it('lists every place under its group, then keeps only what the search matches', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    expect([...page.querySelectorAll('.ng-h')].map((h) => h.textContent.trim())).toEqual([
      'Workspaces',
      'Administration',
    ]);
    expect(page.querySelector('a.ni')?.getAttribute('href')).toBe('/');

    const search = page.querySelector<HTMLInputElement>('input');
    if (search === null) throw new Error('no search box');
    search.value = 'use';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    expect([...page.querySelectorAll('a.ni')].map((a) => a.textContent.trim())).toEqual(['Users']);

    search.value = 'zzz';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    expect(page.textContent).toContain('No place matches');
  });

  it('as the command palette: Enter goes to the first match, ArrowDown moves focus into the results', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const search = page.querySelector<HTMLInputElement>('input');
    if (search === null) throw new Error('no search box');
    search.value = 'use';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    search.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    expect(navigate).toHaveBeenCalledWith(['/p', 'GRD', 'admin']);
    const link = page.querySelector<HTMLElement>('a.ni');
    if (link === null) throw new Error('no result link');
    // jsdom cannot focus inside a closed popover; the call is the unit, the real focus is the page gate's proof.
    const focus = vi.spyOn(link, 'focus');
    search.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    expect(focus).toHaveBeenCalledOnce();
  });
});
