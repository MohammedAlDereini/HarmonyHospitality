import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SecurityRulesStore } from '@harmony/administration/data-access';
import { ApprovalRequest, CustomerRules } from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { InstantFormatter } from '@harmony/platform/context';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { SecurityRulesPage } from './security-rules-page';

const OK = 200;
const ACCEPTED = 202;
const MOST_DAYS = 90;
const UUID_LENGTH = 36;
// Runs of whitespace collapse to one space: matches "a \n b" (→ "a b"); rejects "ab".
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

const merge: ApprovalRequest = {
  id: 'q1',
  kind: 'account.merge',
  requestedById: 'u1',
  requestedBy: 'Ahmad Saleh',
  targetId: 'u5',
  target: 'Rana Qasem',
  raisedAtUtc: '2026-10-27T08:00:00Z',
};

class FakeStore {
  readonly rules = signal<CustomerRules | null>({
    approvalsRequired: true,
    inactivityDays: MOST_DAYS,
  });
  readonly waiting = signal<readonly ApprovalRequest[]>([merge]);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  readonly calls: string[] = [];
  answer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  reload(): void {
    this.calls.push('reload');
  }
  approve(request: ApprovalRequest, k: IntentKey): Promise<ApiResult<unknown>> {
    this.calls.push(`approve:${request.id}:${String(k.value.length)}`);
    return Promise.resolve(this.answer);
  }
  reject(request: ApprovalRequest): Promise<ApiResult<unknown>> {
    this.calls.push(`reject:${request.id}`);
    return Promise.resolve(this.answer);
  }
  setApprovals(on: boolean): Promise<ApiResult<unknown>> {
    this.calls.push(`approvals:${String(on)}`);
    return Promise.resolve(this.answer);
  }
  setInactivityDays(days: number): Promise<ApiResult<unknown>> {
    this.calls.push(`days:${String(days)}`);
    return Promise.resolve(this.answer);
  }
}

describe('SecurityRulesPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<SecurityRulesPage>;
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
      providers: [
        { provide: AccessStore, useValue: { can: () => mayManage } },
        { provide: InstantFormatter, useValue: { formatInstant: () => 'Oct 27, 2026, 08:00' } },
      ],
    });
    TestBed.overrideComponent(SecurityRulesPage, {
      set: { providers: [{ provide: SecurityRulesStore, useValue: store }] },
    });
    fixture = TestBed.createComponent(SecurityRulesPage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  };

  beforeEach(async () => {
    store = new FakeStore();
    mayManage = true;
    await render();
  });

  it('shows approvals on and what waits: the kind, the person, who asked', () => {
    expect(texts('[data-approvals] .sec-h .obj b')).toEqual(['On']);
    expect(texts('[data-request] .s-nm b')).toEqual(['Merging two accounts']);
    expect(texts('[data-request] .s-nm span')).toEqual(['Rana Qasem']);
    expect(texts('[data-request] .obj b')).toEqual(['Ahmad Saleh']);
    expect(page.querySelector<HTMLInputElement>('#inactive-days')?.value).toBe(String(MOST_DAYS));
  });

  it('Approve is one press with a fresh key; Reject too; a refusal is worded once', async () => {
    click('[data-request] [data-act="approve"]');
    await settle();
    store.answer = { kind: 'refused', status: ACCEPTED, codes: ['00132'] };
    click('[data-request] [data-act="reject"]');
    await settle();
    expect(store.calls).toEqual([`approve:q1:${String(UUID_LENGTH)}`, 'reject:q1']);
    expect(texts('.empty[role="alert"]')).toEqual([
      'You asked for this change, so another administrator must decide it.',
    ]);
  });

  it('Switch off while approvals are on goes for approval: a toast, and the page reads again', async () => {
    store.answer = { kind: 'refused', status: ACCEPTED, codes: ['00131'] };
    click('[data-act="approvals-off"]');
    await settle();
    expect(store.calls).toEqual(['approvals:false', 'reload']);
    expect(page.querySelector('.empty[role="alert"]')).toBeNull();
  });

  it('days: more than 90 is stopped here; 30 is saved', async () => {
    await type('#inactive-days', '120');
    page.querySelector<HTMLFormElement>('[data-inactive] form')?.requestSubmit();
    await settle();
    expect(store.calls).toEqual([]);
    expect(texts('[data-inactive] .field-error')).toEqual(['Choose 1 to 90 days.']);
    await type('#inactive-days', '30');
    page.querySelector<HTMLFormElement>('[data-inactive] form')?.requestSubmit();
    await settle();
    expect(store.calls).toEqual(['days:30']);
  });

  it('without ManageUsers: nothing to press or type', async () => {
    TestBed.resetTestingModule();
    mayManage = false;
    await render();
    expect(page.querySelector('[data-act]')).toBeNull();
    expect(page.querySelector('#inactive-days')).toBeNull();
  });
});
