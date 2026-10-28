import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { InvitationsStore } from '@harmony/administration/data-access';
import { Invitation } from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { InstantFormatter } from '@harmony/platform/context';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { InvitationsPage } from './invitations-page';

const OK = 200;
const ACCEPTED = 202;
const UUID_LENGTH = 36;
// Runs of whitespace collapse to one space: matches "a \n b" (→ "a b"); rejects "ab".
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

const omar: Invitation = {
  id: 'i1',
  userAccountId: 'u7',
  displayName: 'Omar Haddad',
  email: 'omar@ritz.test',
  sentBy: 'Ahmad Saleh',
  sentAtUtc: '2026-10-27T08:00:00Z',
  expiresAtUtc: '2026-10-28T08:00:00Z',
  isExpired: false,
};
const huda: Invitation = {
  ...omar,
  id: 'i2',
  userAccountId: 'u8',
  displayName: 'Huda Nasser',
  isExpired: true,
};

class FakeStore {
  readonly rows = signal<readonly Invitation[]>([omar, huda]);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  readonly calls: string[] = [];
  answer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  reload(): void {
    this.calls.push('reload');
  }
  resend(invitation: Invitation, k: IntentKey): Promise<ApiResult<unknown>> {
    this.calls.push(`resend:${invitation.userAccountId}:${String(k.value.length)}`);
    return Promise.resolve(this.answer);
  }
  cancel(invitation: Invitation): Promise<ApiResult<unknown>> {
    this.calls.push(`cancel:${invitation.userAccountId}`);
    return Promise.resolve(this.answer);
  }
}

describe('InvitationsPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<InvitationsPage>;
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
  const render = async (): Promise<void> => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AccessStore, useValue: { can: () => mayManage } },
        { provide: InstantFormatter, useValue: { formatInstant: () => 'Oct 27, 2026, 08:00' } },
      ],
    });
    TestBed.overrideComponent(InvitationsPage, {
      set: { providers: [{ provide: InvitationsStore, useValue: store }] },
    });
    fixture = TestBed.createComponent(InvitationsPage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  };

  beforeEach(async () => {
    store = new FakeStore();
    mayManage = true;
    await render();
  });

  it('lists who waits, who sent it and whether the link still works', () => {
    expect(texts('[data-invitation] .s-nm b')).toEqual(['Omar Haddad', 'Huda Nasser']);
    expect(texts('[data-invitation]:first-child .obj b')[0]).toBe('Ahmad Saleh');
    expect(texts('[data-invitation] [data-s="warn"] b')).toEqual(['Expired']);
  });

  it('Send again is one press with a fresh key; a refusal is worded once', async () => {
    store.answer = { kind: 'refused', status: ACCEPTED, codes: ['00129'] };
    click('[data-invitation] [data-act="resend"]');
    await settle();
    expect(store.calls).toEqual([`resend:u7:${String(UUID_LENGTH)}`]);
    expect(texts('.empty[role="alert"]')).toEqual([
      'This person already chose a password; there is no invitation to send or cancel.',
    ]);
  });

  it('Cancel asks first; Keep it changes nothing, the go button cancels', async () => {
    click('[data-invitation] [data-act="cancel"]');
    await settle();
    click('dialog[open] .dlg-a .btn:not(.solid)');
    await settle();
    expect(store.calls).toEqual([]);
    click('[data-invitation] [data-act="cancel"]');
    await settle();
    click('dialog[open] .dlg-a .solid');
    await settle();
    expect(store.calls).toEqual(['cancel:u7']);
  });

  it('without ManageUsers: nothing to press; none waiting says so', async () => {
    TestBed.resetTestingModule();
    mayManage = false;
    await render();
    expect(page.querySelector('[data-act]')).toBeNull();
    store.rows.set([]);
    await settle();
    expect(texts('.empty')).toEqual(['No invitation is waiting.']);
  });
});
