import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TenantDirectoryStore } from '@harmony/platform-admin/data-access';
import { TenantDirectory } from '@harmony/platform-admin/domain';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult } from '@harmony/platform/http';
import { DirectoryPanel } from './directory-panel';

const OK = 200;
const ACCEPTED = 202;
const THREE_PEOPLE = 3;
// Runs of whitespace collapse to one space: matches "a \n b" (→ "a b"); rejects "ab".
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

const ritz: TenantDirectory = {
  id: 'c1',
  clientId: 'a1b2',
  isEnabled: true,
  secretIssuedAtUtc: '2026-10-27T08:00:00Z',
  people: THREE_PEOPLE,
  groups: [
    { id: 'g1', displayName: 'Front office', roleId: 'r1', members: THREE_PEOPLE },
    { id: 'g2', displayName: 'Everyone', roleId: null, members: THREE_PEOPLE },
  ],
};

class FakeStore {
  readonly directory = signal<TenantDirectory | null>(ritz);
  readonly problem = signal<string | null>(null);
  readonly calls: string[] = [];
  answer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  ofTenant(id: string): void {
    this.calls.push(`of:${id}`);
  }
  remove(): Promise<ApiResult<unknown>> {
    this.calls.push('remove');
    return Promise.resolve(this.answer);
  }
}

describe('DirectoryPanel', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<DirectoryPanel>;
  let panel: HTMLElement;
  let mayManage = true;

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const texts = (selector: string): string[] =>
    [...panel.querySelectorAll(selector)].map((node) =>
      node.textContent.replace(whitespace, ' ').trim(),
    );
  const click = (selector: string): void => {
    panel.querySelector<HTMLElement>(selector)?.click();
  };
  const render = async (): Promise<void> => {
    TestBed.configureTestingModule({
      providers: [{ provide: AccessStore, useValue: { can: () => mayManage } }],
    });
    TestBed.overrideComponent(DirectoryPanel, {
      set: { providers: [{ provide: TenantDirectoryStore, useValue: store }] },
    });
    fixture = TestBed.createComponent(DirectoryPanel);
    fixture.componentRef.setInput('tenantId', 't1');
    panel = fixture.nativeElement as HTMLElement;
    await settle();
  };

  beforeEach(async () => {
    store = new FakeStore();
    mayManage = true;
    await render();
  });

  it('names the customer to the store; shows on, people, groups and what each gives', () => {
    expect(store.calls).toEqual(['of:t1']);
    expect(texts('[data-directory] .obj b')).toEqual([
      'On',
      String(THREE_PEOPLE),
      String(ritz.groups.length),
    ]);
    expect(texts('[data-group] .s-nm span')).toEqual(['Gives a role', 'Gives nothing']);
  });

  it('Remove asks first; Keep it changes nothing; a refusal is worded', async () => {
    click('[data-act="remove-directory"]');
    await settle();
    click('dialog[open] .dlg-a .btn:not(.solid)');
    await settle();
    expect(store.calls).toEqual(['of:t1']);
    store.answer = { kind: 'refused', status: ACCEPTED, codes: ['00140'] };
    click('[data-act="remove-directory"]');
    await settle();
    click('dialog[open] .dlg-a .solid');
    await settle();
    expect(store.calls).toEqual(['of:t1', 'remove']);
    expect(texts('.field-error')).toEqual(['No company directory is connected.']);
  });

  it('none: says the customer opens it; without ManageTenants nothing to press', async () => {
    store.directory.set(null);
    await settle();
    expect(texts('.line').join(' ')).toContain('opens it in Harmony');
    TestBed.resetTestingModule();
    store = new FakeStore();
    mayManage = false;
    await render();
    expect(panel.querySelector('[data-act]')).toBeNull();
  });
});
