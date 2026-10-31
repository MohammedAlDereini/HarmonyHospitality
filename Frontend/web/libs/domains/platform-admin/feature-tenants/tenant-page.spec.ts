import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import {
  CompanyLoginsStore,
  GroupsStore,
  PropertiesStore,
  ReferenceStore,
  TenantDirectoryStore,
  TenantsStore,
} from '@harmony/platform-admin/data-access';
import { SubscriptionLine, Tenant, TenantStatus } from '@harmony/platform-admin/domain';
import { TrailStore } from '@harmony/platform-operations/data-access';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { TenantPage } from './tenant-page';

const OK = 200;
const ACCEPTED = 202;
const TIER_TWO = 2;
const SECOND_READ = 2;
const SIX_HOTELS = 6;
const LICENSED = 740;
const IN_USE = 712;
const PEOPLE = 218;
const WITH_PASSWORD = 41;
const EXCEPTIONS = 3;
const TERMINALS = 14;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const frontOffice: SubscriptionLine = {
  id: 'l1',
  tenantId: 't1',
  propertyId: 'p1',
  moduleCode: 'FO',
  isEnabled: true,
  disabledReason: null,
  renewsAutomatically: true,
  rooms: null,
  terminals: null,
  startsOn: '2026-10-24',
  endsOn: null,
  isHeld: true,
};
const epos: SubscriptionLine = {
  ...frontOffice,
  id: 'l2',
  propertyId: null,
  moduleCode: 'EPOS',
  terminals: TERMINALS,
  renewsAutomatically: false,
  endsOn: '2027-01-01',
};
const ritz: Tenant = {
  id: 't1',
  code: 'ritz',
  externalIdentifier: 'LEV-2026-0031',
  legalNameEn: 'Ritz Amman Hotels LLC',
  legalNameAr: 'فنادق ريتز عمان',
  countryCode: 'JO',
  defaultCurrency: 'JOD',
  status: TenantStatus.active,
  statusReason: null,
  tier: TIER_TWO,
  onboardedOn: '2026-10-24',
  isPlatform: false,
  hotels: SIX_HOTELS,
  roomsInUse: IN_USE,
  licensedRooms: LICENSED,
  moduleCodes: ['EPOS', 'FO'],
  people: PEOPLE,
  peopleWithPassword: WITH_PASSWORD,
  twoStepExceptions: EXCEPTIONS,
  administrator: {
    userAccountId: 'u1',
    displayName: 'Lina Nasser',
    email: 'lina@ritz.test',
    state: 'Active',
    invitationPending: false,
    invitationExpired: false,
    invitedOn: '2026-03-12T08:00:00Z',
    twoStepEnrolled: true,
    twoStepMethod: 'AuthenticatorApp',
    lastSignInUtc: '2026-10-29T11:52:00Z',
    lastSignInMethods: 'pwd mfa',
  },
  subscriptions: [frontOffice, epos],
};

class FakeStore {
  tenant: Tenant = ritz;
  answer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  calls: string[] = [];
  reads = 0;
  detail(): Promise<ApiResult<Tenant>> {
    this.reads += 1;
    return Promise.resolve({ kind: 'ok', status: OK, data: this.tenant });
  }
  suspend(_tenant: Tenant, reason: string, key: IntentKey): Promise<ApiResult<unknown>> {
    this.calls.push(`suspend:${reason}:${String(key.value.length)}`);
    return Promise.resolve(this.answer);
  }
  reinstate(): Promise<ApiResult<unknown>> {
    this.calls.push('reinstate');
    return Promise.resolve(this.answer);
  }
  invite(_tenant: Tenant, mail: string, name: string): Promise<ApiResult<unknown>> {
    this.calls.push(`invite:${mail}:${name}`);
    return Promise.resolve(this.answer);
  }
  unsubscribe(
    _tenant: Tenant,
    line: { moduleCode: string },
    reason: string,
  ): Promise<ApiResult<unknown>> {
    this.calls.push(`end:${line.moduleCode}:${reason}`);
    return Promise.resolve(this.answer);
  }
}

describe('TenantPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<TenantPage>;
  let page: HTMLElement;
  const tenantsOfProperties: (string | null)[] = [];
  const tenantsOfTrail: (string | null)[] = [];

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const click = (selector: string): void => {
    page.querySelector<HTMLElement>(selector)?.click();
  };
  const text = (selector: string): string =>
    page.querySelector(selector)?.textContent.replace(whitespace, ' ').trim() ?? '';
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) =>
      cell.textContent.replace(whitespace, ' ').trim(),
    );
  const typeReason = async (reason: string): Promise<void> => {
    const box = page.querySelector<HTMLInputElement>('#reason-box');
    if (box === null) throw new Error('no reason box');
    box.value = reason;
    box.dispatchEvent(new Event('input'));
    await settle();
    page.querySelector<HTMLFormElement>('hr-reason-form form')?.requestSubmit();
    await settle();
  };

  beforeEach(async () => {
    store = new FakeStore();
    tenantsOfProperties.length = 0;
    tenantsOfTrail.length = 0;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: TenantsStore, useValue: store },
        {
          provide: PropertiesStore,
          useValue: {
            rows: signal([]),
            reload: () => undefined,
            ofTenant: (id: string | null) => {
              tenantsOfProperties.push(id);
            },
          },
        },
        { provide: GroupsStore, useValue: { rows: signal([]), ofTenant: () => undefined } },
        {
          provide: ReferenceStore,
          useValue: {
            choices: () => ({
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
    TestBed.overrideComponent(TenantPage, {
      set: {
        providers: [
          {
            provide: CompanyLoginsStore,
            useValue: {
              rows: signal([
                { provider: 'Microsoft', isEnabled: true, domains: ['ritz.test', 'ritz.jo'] },
              ]),
              problem: signal(null),
              ofTenant: () => undefined,
            },
          },
          {
            provide: TrailStore,
            useValue: {
              rows: signal([]),
              problem: signal(null),
              pageSize: signal(0),
              showTenant: (id: string | null) => {
                tenantsOfTrail.push(id);
              },
            },
          },
          {
            provide: TenantDirectoryStore,
            useValue: {
              directory: signal({ isEnabled: true, people: PEOPLE, groups: [{}, {}] }),
              problem: signal(null),
              ofTenant: () => undefined,
            },
          },
        ],
      },
    });
    fixture = TestBed.createComponent(TenantPage);
    fixture.componentRef.setInput('id', 't1');
    page = fixture.nativeElement as HTMLElement;
    await settle();
  });

  it('reads the tenant and its hotels; the head names it with its code, status and tier; the overview cards say the facts', () => {
    expect(store.reads).toBe(1);
    expect(tenantsOfProperties).toEqual(['t1']);
    expect(tenantsOfTrail).toEqual(['t1']);
    expect(text('h1')).toContain('Ritz Amman Hotels LLC');
    expect(text('.code-tile')).toBe('ritz');
    expect(text('[data-status]')).toBe('Active');
    expect(texts('.ph-title .chip')).toEqual(['Active', 'Tier 2']);
    expect(text('.lead')).toBe(
      'JO · JOD · onboarded 24 Oct 2026 · external id LEV-2026-0031 · 6 hotels · 740 licensed rooms · 218 people',
    );
    expect(texts('.c-tabs [role="tab"]')).toEqual([
      'Overview',
      'Hotels · 0',
      'Groups · 0',
      'Subscriptions · 2',
      'Company login',
      'Directory',
      'People · 218',
      'Trail',
    ]);
    const facts = texts('.cards .c-facts dd');
    expect(facts).toEqual([
      'ADMINISTRATOR (system role, born with the tenant)',
      'On · authenticator app',
      '29 Oct 2026, 11:52 UTC · pwd mfa',
      'Accepted',
      '2 of 5',
      '740 across 6 hotels · 712 in use',
      'EPOS chain-wide · FO on ? only',
      '01 Jan 2027 · fixed term',
      '14 EPOS terminals licensed',
      'Microsoft · enabled · ritz.test, ritz.jo',
      'SCIM push on · 218 people · 2 groups',
      'Required · 3 exceptions',
      '41 of 218 people keep a password',
    ]);
    expect(texts('tr.lrow td.key .nm')).toEqual(['FO', 'EPOS']);
    expect(texts('tr.lrow td.scope')).toEqual(['One hotel', 'Chain-wide · all 6 hotels']);
    expect(texts('tr.lrow .chip')).toEqual(['Active', 'Ends 1 Jan']);
    expect(page.querySelector('[data-act="suspend"]')).not.toBeNull();
    expect(page.querySelector('[data-act="reinstate"]')).toBeNull();
  });

  it('the tabs switch the body: People says the counts and the limit; Subscriptions shows the lines alone', async () => {
    click('.c-tabs [role="tab"]:nth-child(7)');
    await settle();
    expect(text('.c-card h3')).toBe('People');
    expect(page.querySelector('table.lines')).toBeNull();
    click('.c-tabs [role="tab"]:nth-child(4)');
    await settle();
    expect(page.querySelector('table.lines')).not.toBeNull();
    expect(page.querySelector('.cards')).toBeNull();
  });

  it('Suspend asks a reason and sends it with a key; the tenant is read again; a refusal is worded', async () => {
    click('[data-act="suspend"]');
    await settle();
    expect(page.querySelector('hr-reason-form')).not.toBeNull();
    store.tenant = { ...ritz, status: TenantStatus.suspended, statusReason: 'unpaid invoices' };
    await typeReason('unpaid invoices');
    // The suspend call with its key length: matches 'suspend:unpaid invoices:36', rejects 'reinstate'. Tester: https://regex101.com/r/5kQy1R/1
    expect(store.calls[0]).toMatch(/^suspend:unpaid invoices:\d+$/);
    expect(store.reads).toBe(SECOND_READ);
    store.answer = { kind: 'refused', status: ACCEPTED, codes: ['00072'] };
    await settle();
    expect(page.querySelector('hr-reason-form')).toBeNull();
    expect(page.querySelector('[data-act="reinstate"]')).not.toBeNull();
    click('[data-act="reinstate"]');
    await settle();
    expect(store.calls.at(-1)).toBe('reinstate');
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('not active');
  });

  it('a URL that asks for a module (?subscribe=<hotel>) opens the Add module drawer once the tenant has rendered', async () => {
    fixture = TestBed.createComponent(TenantPage);
    fixture.componentRef.setInput('id', 't1');
    fixture.componentRef.setInput('subscribe', 'p1');
    page = fixture.nativeElement as HTMLElement;
    await settle();
    await settle();
    expect(page.querySelector('hr-subscription-editor')).not.toBeNull();
    expect(text('hr-drawer [id$="-heading"]')).toBe('Add module');
  });

  it('the invitation form sends the email and the name; End on a line asks a reason', async () => {
    click('[data-act="invite"]');
    await settle();
    const mail = page.querySelector<HTMLInputElement>('#invite-email');
    const name = page.querySelector<HTMLInputElement>('#invite-name');
    if (mail === null || name === null) throw new Error('no invitation form');
    mail.value = 'huda@ritz.test';
    mail.dispatchEvent(new Event('input'));
    name.value = 'Huda Nabil';
    name.dispatchEvent(new Event('input'));
    await settle();
    page.querySelector<HTMLFormElement>('form.editor')?.requestSubmit();
    await settle();
    expect(store.calls).toEqual(['invite:huda@ritz.test:Huda Nabil']);
    expect(page.querySelector('[role="status"]')?.textContent).toContain('invitation went out');
    click('[data-act="end-line"]');
    await settle();
    await typeReason('no longer needed');
    expect(store.calls.at(-1)).toBe('end:FO:no longer needed');
  });
});
