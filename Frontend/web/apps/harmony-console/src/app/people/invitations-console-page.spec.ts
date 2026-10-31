import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { InvitationsStore, RolesStore, UsersStore } from '@harmony/administration/data-access';
import { AccountState, Invitation, UserAccount } from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult } from '@harmony/platform/http';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { InvitationsConsolePage } from './invitations-console-page';

const OK = 200;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const yousef: Invitation = {
  id: 'i1',
  userAccountId: 'u3',
  displayName: 'Yousef Karam',
  email: 'yousef.karam@harmony.example',
  sentBy: 'Omar Haddad',
  sentAtUtc: '2026-10-28T16:10:00Z',
  expiresAtUtc: '2026-10-29T16:10:00Z',
  isExpired: false,
};
const maya: Invitation = {
  id: 'i2',
  userAccountId: 'u4',
  displayName: 'Maya Darwish',
  email: 'maya.darwish@harmony.example',
  sentBy: 'Lina Nasser',
  sentAtUtc: '2026-10-26T09:00:00Z',
  expiresAtUtc: '2026-10-27T09:00:00Z',
  isExpired: true,
};
const account = (id: string, roleId: string): UserAccount => ({
  id,
  userName: `${id}@harmony.example`,
  email: `${id}@harmony.example`,
  displayName: id,
  twoFactorEnabled: false,
  twoStepExemptReason: null,
  state: AccountState.active,
  stateReason: null,
  isDirectoryManaged: false,
  createdDate: '2026-10-26T09:00:00',
  lastSignInUtc: null,
  lastSignInDevice: null,
  roleIds: [roleId],
});

class FakeStore {
  readonly rows = signal<readonly Invitation[]>([yousef, maya]);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  calls: string[] = [];
  reload(): void {
    this.calls.push('reload');
  }
  resend(invitation: Invitation): Promise<ApiResult<unknown>> {
    this.calls.push(`resend:${invitation.id}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
  cancel(invitation: Invitation): Promise<ApiResult<unknown>> {
    this.calls.push(`cancel:${invitation.id}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
}

describe('InvitationsConsolePage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<InvitationsConsolePage>;
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
        { provide: InvitationsStore, useValue: store },
        {
          provide: UsersStore,
          useValue: {
            rows: signal([]),
            total: signal(0),
            census: signal(null),
            everyone: signal([account('u3', 'r1'), account('u4', 'r2')]),
            countAll: () => undefined,
          },
        },
        {
          provide: RolesStore,
          useValue: {
            roles: signal([
              { id: 'r1', code: 'SUPPORT', nameEn: 'Support', nameAr: null },
              { id: 'r2', code: 'BILLING', nameEn: 'Billing', nameAr: null },
            ]),
            loading: signal(false),
            problem: signal(null),
            load: () => undefined,
          },
        },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
    fixture = TestBed.createComponent(InvitationsConsolePage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  });

  it('lists who was invited, by whom, when, whether the link still works and the role on arrival', () => {
    expect(page.querySelector('.c-tabs a[aria-current="page"]')?.textContent.trim()).toBe(
      'Invitations · 2',
    );
    const rows = [...page.querySelectorAll('tr.irow')].map((row) =>
      [...row.querySelectorAll('td')]
        .slice(0, -1)
        .map((cell) => cell.textContent.replace(whitespace, ' ').trim()),
    );
    expect(rows).toEqual([
      [
        'Yousef Karam yousef.karam@harmony.example',
        'Omar Haddad',
        'Oct 28, 2026 16:10 UTC',
        'valid until Oct 29, 2026 16:10 UTC',
        'Support',
      ],
      [
        'Maya Darwish maya.darwish@harmony.example',
        'Lina Nasser',
        'Oct 26, 2026 09:00 UTC',
        'Expired Oct 27, 2026 09:00 UTC',
        'Billing',
      ],
    ]);
    expect(texts('tr.irow .chip[data-s="warn"]')).toEqual(['Expired Oct 27, 2026 09:00 UTC']);
    expect(texts('.c-filters .count')).toEqual(['2 waiting · by sent']);
  });

  it('the Link facet narrows the list on the page; Send again goes to the store', async () => {
    const facet = page.querySelector('hr-facet');
    facet?.querySelector<HTMLInputElement>('input[value="expired"]')?.click();
    await settle();
    expect(texts('tr.irow .nm')).toEqual(['Maya Darwish']);
    page.querySelector<HTMLButtonElement>('tr.irow [data-act="resend"]')?.click();
    await settle();
    expect(store.calls).toEqual(['resend:i2']);
  });
});
