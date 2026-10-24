import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PropertiesStore, ReferenceStore, TenantsStore } from '@harmony/platform-admin/data-access';
import { SubscriptionLine, Tenant, TenantStatus } from '@harmony/platform-admin/domain';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { TenantPage } from './tenant-page';

const OK = 200;
const ACCEPTED = 202;
const TIER_TWO = 2;
const SECOND_READ = 2;

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
  isHeld: false,
};
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
  subscriptions: [frontOffice],
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

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const click = (selector: string): void => {
    page.querySelector<HTMLElement>(selector)?.click();
  };
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
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: TenantsStore, useValue: store },
        {
          provide: PropertiesStore,
          useValue: {
            rows: signal([]),
            ofTenant: (id: string | null) => {
              tenantsOfProperties.push(id);
            },
          },
        },
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
    fixture = TestBed.createComponent(TenantPage);
    fixture.componentRef.setInput('id', 't1');
    page = fixture.nativeElement as HTMLElement;
    await settle();
  });

  it('reads the tenant and its properties; shows the header and the subscription lines', () => {
    expect(store.reads).toBe(1);
    expect(tenantsOfProperties).toEqual(['t1']);
    expect(page.querySelector('.head h1')?.textContent.trim()).toBe('Ritz Amman Hotels LLC');
    expect(page.querySelector('[data-status]')?.textContent).toBe('Active');
    expect(page.querySelector('tr.lrow td.key')?.textContent).toBe('FO');
    expect(page.querySelector('tr.lrow .nm')?.textContent).toBe('One property');
    expect(page.querySelector('[data-act="suspend"]')).not.toBeNull();
    expect(page.querySelector('[data-act="reinstate"]')).toBeNull();
  });

  it('Suspend asks a reason and sends it with a key; the tenant is read again; a refusal is worded', async () => {
    click('[data-act="suspend"]');
    await settle();
    // The press reads the tenant again: the fake answers the suspended one from then on.
    store.tenant = { ...ritz, status: TenantStatus.suspended };
    await typeReason('unpaid invoices');
    expect(store.calls).toEqual(['suspend:unpaid invoices:36']);
    expect(store.reads).toBe(SECOND_READ);
    expect(page.querySelector('hr-reason-form')).toBeNull();
    store.answer = { kind: 'refused', status: ACCEPTED, codes: ['00072'] };
    click('[data-act="reinstate"]');
    await settle();
    expect(store.calls).toContain('reinstate');
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('not active');
  });

  it('the invitation form sends the email and the name; End on a line asks a reason', async () => {
    click('[data-act="invite"]');
    await settle();
    const mail = page.querySelector<HTMLInputElement>('#invite-email');
    const name = page.querySelector<HTMLInputElement>('#invite-name');
    if (mail === null || name === null) throw new Error('no invite form');
    mail.value = 'owner@ritz.test';
    mail.dispatchEvent(new Event('input'));
    name.value = 'Amal Haddad';
    name.dispatchEvent(new Event('input'));
    await settle();
    page.querySelector<HTMLFormElement>('form.editor')?.requestSubmit();
    await settle();
    expect(store.calls).toEqual(['invite:owner@ritz.test:Amal Haddad']);
    expect(page.textContent).toContain('invitation went out');
    click('[data-act="end-line"]');
    await settle();
    await typeReason('module no longer used');
    expect(store.calls).toContain('end:FO:module no longer used');
  });
});
