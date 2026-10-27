import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CompanyLoginStore } from '@harmony/administration/data-access';
import { CompanyLogin } from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { CompanyLoginPage } from './company-login-page';

const OK = 200;
const ACCEPTED = 202;
const UUID_LENGTH = 36;
// Runs of whitespace collapse to one space: matches "a \n b" (→ "a b"); rejects "ab".
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

const microsoft: CompanyLogin = {
  id: 'cl1',
  provider: 'Microsoft',
  issuer: 'https://login.microsoftonline.com/0f1c2d3e-0000-4000-8000-000000000001/v2.0',
  isEnabled: false,
  domains: ['ritz.com', 'ritz-hotels.com'],
  consentUrl:
    'https://login.microsoftonline.com/0f1c2d3e-0000-4000-8000-000000000001/adminconsent?client_id=app',
  modifiedDate: '2026-10-27T08:00:00Z',
};

class FakeStore {
  readonly rows = signal<readonly CompanyLogin[]>([microsoft]);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  readonly calls: string[] = [];
  answer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  reload(): void {
    this.calls.push('reload');
  }
  addDomain(login: CompanyLogin, domain: string, k: IntentKey): Promise<ApiResult<unknown>> {
    this.calls.push(`add:${login.id}:${domain}:${String(k.value.length)}`);
    return Promise.resolve(this.answer);
  }
  removeDomain(login: CompanyLogin, domain: string): Promise<ApiResult<unknown>> {
    this.calls.push(`remove:${login.id}:${domain}`);
    return Promise.resolve(this.answer);
  }
  switch(login: CompanyLogin, on: boolean): Promise<ApiResult<unknown>> {
    this.calls.push(`switch:${login.id}:${String(on)}`);
    return Promise.resolve(this.answer);
  }
}

describe('CompanyLoginPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<CompanyLoginPage>;
  let page: HTMLElement;
  let mayManage = true;

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((node) =>
      node.textContent.replace(whitespace, ' ').trim(),
    );
  const click = (selector: string): void => {
    page.querySelector<HTMLElement>(selector)?.click();
  };
  const type = async (selector: string, value: string): Promise<void> => {
    const box = page.querySelector<HTMLInputElement>(selector);
    if (box === null) throw new Error(`no ${selector}`);
    box.value = value;
    box.dispatchEvent(new Event('input'));
    await settle();
  };
  const render = async (): Promise<void> => {
    TestBed.configureTestingModule({
      providers: [{ provide: AccessStore, useValue: { can: () => mayManage } }],
    });
    // The page provides its own CompanyLoginStore; the fake takes that slot.
    TestBed.overrideComponent(CompanyLoginPage, {
      set: { providers: [{ provide: CompanyLoginStore, useValue: store }] },
    });
    fixture = TestBed.createComponent(CompanyLoginPage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  };

  beforeEach(async () => {
    store = new FakeStore();
    mayManage = true;
    await render();
  });

  it('shows the provider, off, its domains and the consent link for the company IT', () => {
    expect(texts('[data-login] h2')).toEqual(['Microsoft']);
    expect(texts('[data-login] .obj b')).toEqual(['Off']);
    expect(texts('[data-domain] .s-nm b')).toEqual(['ritz.com', 'ritz-hotels.com']);
    expect(page.querySelector('a[target="_blank"]')?.getAttribute('href')).toBe(
      microsoft.consentUrl,
    );
  });

  it('Switch on asks first; Not now changes nothing, Switch on switches', async () => {
    click('[data-act="switch-on"]');
    await settle();
    click('dialog[open] .dlg-a .btn:not(.solid)');
    await settle();
    expect(store.calls).toEqual([]);
    click('[data-act="switch-on"]');
    await settle();
    click('dialog[open] .dlg-a .solid');
    await settle();
    expect(store.calls).toEqual(['switch:cl1:true']);
  });

  it('adds a domain with a fresh key; a taken domain is worded under the field', async () => {
    click('[data-act="add-domain"]');
    await settle();
    await type('#new-domain', ' ritz-group.com ');
    store.answer = { kind: 'refused', status: ACCEPTED, codes: ['00123'] };
    page.querySelector<HTMLFormElement>('form.editor')?.requestSubmit();
    await settle();
    expect(store.calls).toEqual([`add:cl1:ritz-group.com:${String(UUID_LENGTH)}`]);
    expect(texts('form.editor .field-error')).toEqual([
      'That e-mail domain is already on a company login list.',
    ]);
  });

  it('removes a domain with one press; a refusal is worded once on the page', async () => {
    store.answer = { kind: 'refused', status: ACCEPTED, codes: ['00126'] };
    click('[data-domain] [data-act="remove-domain"]');
    await settle();
    expect(store.calls).toEqual(['remove:cl1:ritz.com']);
    expect(texts('.empty[role="alert"]')).toEqual([
      'Company sign-in needs at least one e-mail domain while it is on.',
    ]);
  });

  it('without ManageUsers: nothing to press', async () => {
    TestBed.resetTestingModule();
    mayManage = false;
    await render();
    expect(page.querySelector('[data-act]')).toBeNull();
  });

  it('none yet: says Harmony adds it', async () => {
    store.rows.set([]);
    await settle();
    expect(texts('.empty').join(' ')).toContain('Ask Harmony to add Microsoft or Google');
  });
});
