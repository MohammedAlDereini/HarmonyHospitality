import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ReferenceStore, TenantsStore } from '@harmony/platform-admin/data-access';
import { Tenant, TenantDraft, TenantStatus } from '@harmony/platform-admin/domain';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { TenantEditor } from './tenant-editor';

const OK = 200;
const ACCEPTED = 202;
const TIER_TWO = 2;

const ritz: Tenant = {
  id: 't1',
  code: 'ritz',
  externalIdentifier: null,
  legalNameEn: 'Ritz Amman Hotels LLC',
  legalNameAr: 'فنادق ريتز عمان',
  countryCode: 'JO',
  defaultCurrency: 'JOD',
  status: TenantStatus.active,
  statusReason: null,
  tier: TIER_TWO,
  onboardedOn: '2026-10-24',
  isPlatform: false,
  subscriptions: [],
  hotels: 0,
  roomsInUse: 0,
  licensedRooms: 0,
  moduleCodes: [],
  people: 0,
  peopleWithPassword: 0,
  twoStepExceptions: 0,
  administrator: null,
};
const jordan = { code: 'JO', nameEn: 'Jordan', nameAr: 'الأردن' };
const dinar = { code: 'JOD', nameEn: 'Jordanian dinar', nameAr: 'دينار' };

@Component({
  selector: 'hr-host',
  imports: [TenantEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hr-tenant-editor [tenant]="tenant()" (done)="closed.set(closed() + 1)" />`,
})
class Host {
  readonly tenant = signal<Tenant | null>(null);
  readonly closed = signal(0);
}

describe('hr-tenant-editor', () => {
  const created: { draft: TenantDraft; key: IntentKey }[] = [];
  const updated: { id: string; draft: TenantDraft }[] = [];
  let answer: ApiResult<unknown>;

  const table = (rows: readonly object[]) => ({
    rows: signal(rows),
    loading: signal(false),
    problem: signal(null),
    scope: signal({}),
    load: () => undefined,
  });

  const render = async (tenant: Tenant | null = null) => {
    created.length = 0;
    updated.length = 0;
    TestBed.configureTestingModule({
      providers: [
        {
          provide: TenantsStore,
          useValue: {
            create: (draft: TenantDraft, key: IntentKey) => {
              created.push({ draft, key });
              return Promise.resolve(answer);
            },
            update: (target: Tenant, draft: TenantDraft) => {
              updated.push({ id: target.id, draft });
              return Promise.resolve(answer);
            },
          },
        },
        {
          provide: ReferenceStore,
          useValue: {
            choices: (controller: string) =>
              controller === 'Country' ? table([jordan]) : table([dinar]),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.tenant.set(tenant);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async (): Promise<void> => {
      await new Promise((resolve) => setTimeout(resolve, 1));
      fixture.detectChanges();
      await fixture.whenStable();
    };
    const type = async (selector: string, value: string): Promise<void> => {
      const box = page.querySelector<HTMLInputElement | HTMLSelectElement>(selector);
      if (box === null) throw new Error(`no ${selector}`);
      box.value = value;
      box.dispatchEvent(new Event('input'));
      box.dispatchEvent(new Event('change'));
      await settle();
    };
    const send = async (): Promise<void> => {
      page.querySelector('form')?.requestSubmit();
      await settle();
    };
    return { fixture, page, type, send };
  };

  it('a new tenant needs its code, names, country, currency and the administrator; then it is created with the key', async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const editor = await render();
    await editor.send();
    expect(created).toEqual([]);
    expect(editor.page.querySelector('#tenant-code-error')?.textContent).toContain('code');
    expect(editor.page.querySelector('#tenant-admin-email-error')?.textContent).toContain('email');
    await editor.type('#tenant-code', ' blue-nile ');
    await editor.type('#tenant-name-en', 'Blue Nile Hotels');
    await editor.type('#tenant-name-ar', 'فنادق النيل الأزرق');
    await editor.type('#tenant-country', 'JO');
    await editor.type('#tenant-currency', 'JOD');
    await editor.type('#tenant-tier', '1');
    await editor.type('#tenant-admin-email', 'owner@bluenile.test');
    await editor.type('#tenant-admin-name', 'Huda Nabil');
    await editor.send();
    expect(created).toHaveLength(1);
    expect(created[0]?.draft).toEqual({
      code: 'blue-nile',
      legalNameEn: 'Blue Nile Hotels',
      legalNameAr: 'فنادق النيل الأزرق',
      countryCode: 'JO',
      defaultCurrency: 'JOD',
      tier: 1,
      externalIdentifier: '',
      administratorEmail: 'owner@bluenile.test',
      administratorDisplayName: 'Huda Nabil',
    });
    expect(created[0]?.key.value).not.toBe('');
    expect(editor.fixture.componentInstance.closed()).toBe(1);
  });

  it('editing: the fields start from the tenant, the code is read-only, no administrator fields; Save updates', async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const editor = await render(ritz);
    expect(editor.page.querySelector<HTMLInputElement>('#tenant-code')?.value).toBe('ritz');
    expect(editor.page.querySelector<HTMLInputElement>('#tenant-code')?.readOnly).toBe(true);
    expect(editor.page.querySelector('#tenant-admin-email')).toBeNull();
    expect(editor.page.querySelector<HTMLSelectElement>('#tenant-tier')?.value).toBe('2');
    await editor.type('#tenant-name-en', 'Ritz Amman Hotels');
    await editor.send();
    expect(updated).toHaveLength(1);
    expect(updated[0]?.id).toBe('t1');
    expect(updated[0]?.draft.legalNameEn).toBe('Ritz Amman Hotels');
  });

  it('a refusal about the code lands under the code field; the form stays open', async () => {
    answer = { kind: 'refused', status: ACCEPTED, codes: ['00071'] };
    const editor = await render();
    await editor.type('#tenant-code', 'ritz');
    await editor.type('#tenant-name-en', 'Ritz Again');
    await editor.type('#tenant-name-ar', 'ريتز');
    await editor.type('#tenant-country', 'JO');
    await editor.type('#tenant-currency', 'JOD');
    await editor.type('#tenant-admin-email', 'owner@ritz.test');
    await editor.type('#tenant-admin-name', 'Amal Haddad');
    await editor.send();
    expect(created).toHaveLength(1);
    expect(editor.page.querySelector('#tenant-code-error')?.textContent).toContain('already used');
    expect(editor.fixture.componentInstance.closed()).toBe(0);
  });
});
