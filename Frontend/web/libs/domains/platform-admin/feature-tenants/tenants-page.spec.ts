import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ReferenceStore, TenantsStore } from '@harmony/platform-admin/data-access';
import { Tenant, TenantFilters, TenantStatus } from '@harmony/platform-admin/domain';
import { AccessStore } from '@harmony/platform/access';
import { PageActions } from '@harmony/ui/shell';
import { TenantsPage } from './tenants-page';

const TIER_TWO = 2;
const TWO_ROWS = 2;

const ritz: Tenant = {
  id: 't1',
  code: 'ritz',
  externalIdentifier: null,
  legalNameEn: 'Ritz Amman Hotels LLC',
  legalNameAr: 'فنادق ريتز عمان',
  countryCode: 'JO',
  defaultCurrency: 'JOD',
  status: TenantStatus.suspended,
  statusReason: 'unpaid',
  tier: TIER_TWO,
  onboardedOn: '2026-10-24',
  isPlatform: false,
  subscriptions: [],
};
const platform: Tenant = {
  ...ritz,
  id: 't0',
  code: 'harmony',
  legalNameEn: 'Harmony',
  status: TenantStatus.active,
  statusReason: null,
  isPlatform: true,
};

class FakeStore {
  readonly filters = signal<TenantFilters>({ search: '', status: null, page: 1 });
  readonly rows = signal<readonly Tenant[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  searched: string[] = [];
  statuses: (TenantStatus | null)[] = [];
  search(text: string): void {
    this.searched.push(text);
  }
  showStatus(status: TenantStatus | null): void {
    this.statuses.push(status);
  }
  goTo(): void {
    return undefined;
  }
  reload(): void {
    return undefined;
  }
}

describe('TenantsPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<TenantsPage>;
  let page: HTMLElement;

  const render = async (): Promise<void> => {
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const text = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) => cell.textContent.trim());

  beforeEach(async () => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: TenantsStore, useValue: store },
        {
          provide: ReferenceStore,
          useValue: {
            table: () => ({
              rows: signal([]),
              loading: signal(false),
              problem: signal(null),
              scope: signal({}),
              load: () => undefined,
            }),
          },
        },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
    fixture = TestBed.createComponent(TenantsPage);
    page = fixture.nativeElement as HTMLElement;
    await render();
  });

  it('lists code, name, country and currency, tier, status, onboarded; the platform is named as such', async () => {
    store.rows.set([ritz, platform]);
    store.total.set(TWO_ROWS);
    await render();
    const first = '[role="listitem"]:first-child';
    const last = '[role="listitem"]:last-child';
    expect(text(`${first} .code`)).toEqual(['ritz']);
    expect(text(`${first} .s-nm b`)).toEqual(['Ritz Amman Hotels LLC']);
    expect(text(`${first} .s-nm span`)).toEqual(['JO · JOD']);
    expect(text(`${first} .obj b`)).toEqual(['2', 'Suspended']);
    expect(page.querySelector(`${first} .obj[data-s="warn"]`)).not.toBeNull();
    expect(page.querySelector(`${first} a.nm-link`)?.getAttribute('href')).toBe('/t1');
    expect(text(`${last} .s-nm span`)).toEqual(['The platform itself']);
  });

  it('the status filter and the search go to the store; Add tenant opens the editor', async () => {
    store.rows.set([ritz]);
    await render();
    page.querySelectorAll<HTMLButtonElement>('.seg button')[1]?.click();
    expect(store.statuses).toEqual([TenantStatus.active]);
    const input = page.querySelector<HTMLInputElement>('#tenants-search');
    if (input === null) throw new Error('no search field');
    input.value = 'ritz';
    page.querySelector('form')?.requestSubmit();
    expect(store.searched).toEqual(['ritz']);
    const offered = TestBed.inject(PageActions).template();
    if (offered === null) throw new Error('nothing offered to the slot');
    const slot = document.createElement('div');
    const view = offered.createEmbeddedView(undefined);
    view.detectChanges();
    view.rootNodes.forEach((node: Node) => slot.appendChild(node));
    slot.querySelector<HTMLButtonElement>('.pbtn')?.click();
    await render();
    expect(page.querySelector('hr-tenant-editor')).not.toBeNull();
    view.destroy();
  });
});
