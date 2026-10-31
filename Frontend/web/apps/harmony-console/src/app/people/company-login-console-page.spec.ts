import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import {
  CompanyLoginStore,
  InvitationsStore,
  RolesStore,
  UsersStore,
} from '@harmony/administration/data-access';
import { AccountState, CompanyLogin, UserAccount } from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult, IntentKey, newIntentKey } from '@harmony/platform/http';
import { IdentityConfirmation, SessionStore } from '@harmony/platform/session';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { CompanyLoginConsolePage } from './company-login-console-page';

const OK = 200;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const microsoft: CompanyLogin = {
  id: 'c1',
  provider: 'Microsoft',
  issuer: 'https://login.microsoftonline.com/x/v2.0',
  isEnabled: true,
  domains: ['harmony.example', 'harmony-pms.example'],
  consentUrl: 'https://login.microsoftonline.com/x/adminconsent',
  modifiedDate: null,
};
const person = (id: string, email: string): UserAccount => ({
  id,
  userName: email,
  email,
  displayName: id,
  twoFactorEnabled: true,
  twoStepExemptReason: null,
  state: AccountState.active,
  stateReason: null,
  isDirectoryManaged: false,
  createdDate: '2026-10-26T09:00:00',
  lastSignInUtc: null,
  lastSignInDevice: null,
  roleIds: [],
});

class FakeStore {
  readonly rows = signal<readonly CompanyLogin[]>([microsoft]);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  calls: string[] = [];
  reload(): void {
    this.calls.push('reload');
  }
  addDomain(_login: CompanyLogin, domain: string): Promise<ApiResult<unknown>> {
    this.calls.push(`add:${domain}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
  removeDomain(_login: CompanyLogin, domain: string): Promise<ApiResult<unknown>> {
    this.calls.push(`remove:${domain}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
  switch(_login: CompanyLogin, on: boolean): Promise<ApiResult<unknown>> {
    this.calls.push(`switch:${String(on)}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
}

describe('CompanyLoginConsolePage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<CompanyLoginConsolePage>;
  let page: HTMLElement;

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) =>
      cell.textContent.replace(whitespace, ' ').trim(),
    );

  beforeEach(async () => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        {
          provide: UsersStore,
          useValue: {
            rows: signal([]),
            total: signal(0),
            census: signal(null),
            everyone: signal([
              person('u1', 'omar@harmony.example'),
              person('u2', 'lina@harmony.example'),
              person('u3', 'ext@partner.example'),
            ]),
            countAll: () => undefined,
          },
        },
        { provide: InvitationsStore, useValue: { rows: signal([]), reload: () => undefined } },
        {
          provide: RolesStore,
          useValue: {
            roles: signal([]),
            loading: signal(false),
            problem: signal(null),
            load: () => undefined,
          },
        },
        { provide: SessionStore, useValue: { user: signal({ tenant: 't-platform' }) } },
        {
          provide: IdentityConfirmation,
          useValue: {
            run: (call: (intent: IntentKey) => Promise<ApiResult<unknown>>) => call(newIntentKey()),
          },
        },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
    TestBed.overrideComponent(CompanyLoginConsolePage, {
      set: { providers: [{ provide: CompanyLoginStore, useValue: store }] },
    });
    fixture = TestBed.createComponent(CompanyLoginConsolePage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  });

  it('one card per provider: the added one with its facts and domains, the missing one pointing at the door', () => {
    expect(texts('[data-login] .c-card-h h3')).toEqual(['Microsoft']);
    expect(texts('[data-login] .c-card-h .chip')).toEqual(['On']);
    expect(texts('[data-login] .c-facts dt')).toEqual([
      'Consent',
      'Approval page',
      'People using it',
    ]);
    expect(texts('[data-login] .c-facts dd')[2]).toBe('2 of 3');
    expect(texts('tr.mrow td:nth-child(1)')).toEqual(['harmony.example', 'harmony-pms.example']);
    expect(texts('tr.mrow td:nth-child(2)')).toEqual(['2', '0']);
    expect(texts('[data-missing] .c-card-h h3')).toEqual(['Google']);
    expect(texts('[data-missing] .chip')).toEqual(['Not added']);
    expect(page.querySelector('[data-missing] a.btn')?.getAttribute('href')).toBe(
      '/tenants/t-platform?tab=company-login',
    );
  });

  it('Switch off and Remove go to the store; Add a domain opens the field', async () => {
    page.querySelector<HTMLButtonElement>('[data-act="switch-off"]')?.click();
    await settle();
    page.querySelector<HTMLButtonElement>('tr.mrow [data-act="remove-domain"]')?.click();
    await settle();
    expect(store.calls).toEqual(['switch:false', 'remove:harmony.example']);
    page.querySelector<HTMLButtonElement>('[data-act="add-domain"]')?.click();
    await settle();
    expect(page.querySelector('#new-domain')).not.toBeNull();
  });
});
