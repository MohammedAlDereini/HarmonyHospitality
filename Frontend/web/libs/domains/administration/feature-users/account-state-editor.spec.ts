import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { UsersStore } from '@harmony/administration/data-access';
import { AccountState, UserAccount } from '@harmony/administration/domain';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { AccountStateEditor } from './account-state-editor';

const OK = 200;
const ACCEPTED = 202;
const BAD_REQUEST = 400;
// A UUID as crypto.randomUUID() writes it: 8-4-4-4-12 lower-case hex. Matches "0af7651916cd43dd8448eb211c80319c"?
// no: that has no dashes and is rejected; matches "3b241101-e2bb-4255-8caf-4136c566a962".
// Tester: https://regex101.com/?regex=%5E%5B0-9a-f-%5D%7B36%7D%24&testString=3b241101-e2bb-4255-8caf-4136c566a962
const uuid = /^[0-9a-f-]{36}$/;

const sami: UserAccount = {
  id: 'u3',
  userName: 'sami@ritz.test',
  email: 'sami@ritz.test',
  displayName: 'Sami Odeh',
  twoFactorEnabled: false,
  twoStepExemptReason: 'night desk',
  state: AccountState.active,
  stateReason: null,
  createdDate: '2026-10-24T08:00:00Z',
};

@Component({
  selector: 'hr-host',
  imports: [AccountStateEditor],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hr-account-state-editor [user]="user" (done)="closed.set(closed() + 1)" />`,
})
class Host {
  readonly user = sami;
  readonly closed = signal(0);
}

describe('hr-account-state-editor', () => {
  const calls: { reason: string; key: IntentKey }[] = [];
  let answer: ApiResult<unknown>;

  const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

  const render = async () => {
    calls.length = 0;
    TestBed.configureTestingModule({
      providers: [
        {
          provide: UsersStore,
          useValue: {
            suspend: (_user: UserAccount, reason: string, key: IntentKey) => {
              calls.push({ reason, key });
              return Promise.resolve(answer);
            },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async (): Promise<void> => {
      await tick();
      fixture.detectChanges();
      await fixture.whenStable();
    };
    const type = async (text: string): Promise<void> => {
      const box = page.querySelector<HTMLInputElement>('#suspend-reason');
      if (box === null) throw new Error('no reason box');
      box.value = text;
      box.dispatchEvent(new Event('input'));
      await settle();
    };
    const send = async (): Promise<void> => {
      page.querySelector('form')?.requestSubmit();
      await settle();
    };
    return { fixture, page, type, send, settle };
  };

  it('an empty reason never leaves the browser: the field says so', async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const editor = await render();
    await editor.send();
    expect(calls).toEqual([]);
    expect(editor.page.querySelector('#suspend-reason-error')?.textContent).toContain(
      'Give a reason',
    );
    expect(editor.page.querySelector('#suspend-reason')?.getAttribute('aria-invalid')).toBe('true');
  });

  it('a reason is sent trimmed with a fresh intent key, and success closes the editor', async () => {
    answer = { kind: 'ok', status: OK, data: undefined };
    const editor = await render();
    await editor.type('  left without notice  ');
    await editor.send();
    expect(calls).toHaveLength(1);
    expect(calls[0]?.reason).toBe('left without notice');
    expect(calls[0]?.key.value).toMatch(uuid);
    expect(editor.fixture.componentInstance.closed()).toBe(1);
  });

  it('a business refusal is worded from the catalogue and the editor stays open', async () => {
    answer = { kind: 'refused', status: ACCEPTED, codes: ['00076'] };
    const editor = await render();
    await editor.type('rank test');
    await editor.send();
    expect(editor.page.textContent).toContain('stands above your own rank');
    expect(editor.fixture.componentInstance.closed()).toBe(0);
  });

  it("the server's field code lands under the field", async () => {
    answer = { kind: 'invalid', status: BAD_REQUEST, codes: ['10304'] };
    const editor = await render();
    await editor.type('a reason the server finds too long');
    await editor.send();
    expect(editor.page.querySelector('#suspend-reason-error')?.textContent).toContain('too long');
  });
});
