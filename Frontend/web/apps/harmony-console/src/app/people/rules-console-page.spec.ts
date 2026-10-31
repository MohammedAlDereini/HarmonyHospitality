import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import {
  InvitationsStore,
  RolesStore,
  SecurityRulesStore,
  UsersStore,
} from '@harmony/administration/data-access';
import { ApprovalRequest, CustomerRules } from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { ApiResult, IntentKey, newIntentKey } from '@harmony/platform/http';
import { IdentityConfirmation } from '@harmony/platform/session';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { RulesConsolePage } from './rules-console-page';

const OK = 200;
const SIXTY_DAYS = 60;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const rules: CustomerRules = {
  approvalsRequired: true,
  inactivityDays: SIXTY_DAYS,
  trustedDevicesRequired: false,
};
const adminRole: ApprovalRequest = {
  id: 'q1',
  kind: 'account.administrator-roles',
  requestedById: 'u1',
  requestedBy: 'Omar Haddad',
  targetId: 'u3',
  target: 'Yousef Karam',
  raisedAtUtc: '2026-10-28T16:12:00Z',
};

class FakeStore {
  readonly rules = signal<CustomerRules | null>(rules);
  readonly waiting = signal<readonly ApprovalRequest[]>([adminRole]);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  calls: string[] = [];
  reload(): void {
    this.calls.push('reload');
  }
  setApprovals(required: boolean): Promise<ApiResult<unknown>> {
    this.calls.push(`approvals:${String(required)}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
  setTrustedDevices(required: boolean): Promise<ApiResult<unknown>> {
    this.calls.push(`devices:${String(required)}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
  setInactivityDays(days: number): Promise<ApiResult<unknown>> {
    this.calls.push(`days:${String(days)}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
  approve(request: ApprovalRequest): Promise<ApiResult<unknown>> {
    this.calls.push(`approve:${request.id}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
  reject(request: ApprovalRequest): Promise<ApiResult<unknown>> {
    this.calls.push(`reject:${request.id}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
}

describe('RulesConsolePage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<RulesConsolePage>;
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
            everyone: signal([]),
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
        {
          provide: IdentityConfirmation,
          useValue: {
            run: (call: (intent: IntentKey) => Promise<ApiResult<unknown>>) => call(newIntentKey()),
          },
        },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
    TestBed.overrideComponent(RulesConsolePage, {
      set: { providers: [{ provide: SecurityRulesStore, useValue: store }] },
    });
    fixture = TestBed.createComponent(RulesConsolePage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  });

  it('says each rule with its switch, what waits for approval, and what Identity enforces', () => {
    expect(texts('.cards .c-card-h h3')).toEqual(['Approvals', 'New devices', 'Inactive accounts']);
    expect(texts('.cards .c-card-h .chip')).toEqual(['On', 'Off']);
    expect(page.querySelector<HTMLInputElement>('[data-act="approvals"]')?.checked).toBe(true);
    expect(page.querySelector<HTMLInputElement>('[data-act="devices"]')?.checked).toBe(false);
    expect(page.querySelector<HTMLInputElement>('#inactive-days')?.value).toBe(String(SIXTY_DAYS));
    expect(texts('tr.wrow td').slice(0, -1)).toEqual([
      'An administrator role for Yousef Karam',
      'Omar Haddad',
      'Oct 28, 2026 16:12 UTC',
    ]);
    expect(texts('[data-enforced] dt')).toEqual(['Password', 'Rate limits', 'Tokens']);
  });

  it('the switches, Approve and Save go to the store, one press each', async () => {
    const approvals = page.querySelector<HTMLInputElement>('[data-act="approvals"]');
    if (approvals === null) throw new Error('no approvals switch');
    approvals.checked = false;
    approvals.dispatchEvent(new Event('change'));
    await settle();
    page.querySelector<HTMLButtonElement>('[data-act="approve"]')?.click();
    await settle();
    page.querySelector<HTMLFormElement>('form.days')?.requestSubmit();
    await settle();
    expect(store.calls).toEqual(['approvals:false', 'approve:q1', `days:${String(SIXTY_DAYS)}`]);
  });
});
