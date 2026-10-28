import { computed, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ApiResult } from '@harmony/platform/http';
import { IdentityConfirmation, SessionStore, SessionUser } from '@harmony/platform/session';
import { ConfirmIdentityDialog } from './confirm-identity-dialog';

const OK = 200;
const ACCEPTED = 202;

const amal: SessionUser = {
  sub: 'u1',
  name: 'Amal Haddad',
  tenant: 't1',
  roles: [],
  amr: ['pwd', 'mfa'],
  logoutUrl: '/bff/logout?sid=abc',
};

class FakeConfirmation {
  readonly waiting = signal(false);
  readonly asking = computed(() => this.waiting());
  readonly passwords: string[] = [];
  dismissed = 0;
  answer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  confirm(password: string): Promise<ApiResult<unknown>> {
    this.passwords.push(password);
    if (this.answer.kind === 'ok') this.waiting.set(false);
    return Promise.resolve(this.answer);
  }
  dismiss(): void {
    this.dismissed += 1;
    this.waiting.set(false);
  }
}

describe('ConfirmIdentityDialog', () => {
  let confirmation: FakeConfirmation;
  let user: ReturnType<typeof signal<SessionUser | null>>;
  let fixture: ComponentFixture<ConfirmIdentityDialog>;
  let host: HTMLElement;

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const dialog = (): HTMLDialogElement | null => host.querySelector('dialog');
  const type = async (value: string): Promise<void> => {
    const box = host.querySelector<HTMLInputElement>('#confirm-identity-password');
    if (box === null) throw new Error('no password box');
    box.value = value;
    box.dispatchEvent(new Event('input'));
    await settle();
  };
  const send = async (): Promise<void> => {
    host.querySelector<HTMLFormElement>('form')?.requestSubmit();
    await settle();
  };

  beforeEach(async () => {
    confirmation = new FakeConfirmation();
    user = signal<SessionUser | null>(amal);
    TestBed.configureTestingModule({
      providers: [
        { provide: IdentityConfirmation, useValue: confirmation },
        { provide: SessionStore, useValue: { user } },
      ],
    });
    fixture = TestBed.createComponent(ConfirmIdentityDialog);
    host = fixture.nativeElement as HTMLElement;
    await settle();
  });

  it('stays closed until asked; opens with an empty password field', async () => {
    expect(dialog()?.hasAttribute('open')).toBe(false);
    confirmation.waiting.set(true);
    await settle();
    expect(dialog()?.hasAttribute('open')).toBe(true);
    expect(host.querySelector<HTMLInputElement>('#confirm-identity-password')?.value).toBe('');
    expect(host.querySelector('#confirm-identity-password')?.getAttribute('autocomplete')).toBe(
      'current-password',
    );
  });

  it('an empty password is stopped here; a wrong one is worded and the field is emptied', async () => {
    confirmation.waiting.set(true);
    await settle();
    await send();
    expect(confirmation.passwords).toEqual([]);
    expect(host.querySelector('[role="alert"]')?.textContent.trim()).toBe('Type your password.');
    confirmation.answer = { kind: 'refused', status: ACCEPTED, codes: ['00044'] };
    await type('not-it');
    await send();
    expect(confirmation.passwords).toEqual(['not-it']);
    expect(host.querySelector('[role="alert"]')?.textContent.trim()).toBe(
      'That password is not right.',
    );
    expect(host.querySelector<HTMLInputElement>('#confirm-identity-password')?.value).toBe('');
    expect(dialog()?.hasAttribute('open')).toBe(true);
  });

  it('the right password closes it; Cancel stays', async () => {
    confirmation.waiting.set(true);
    await settle();
    await type('the-right-one');
    await send();
    expect(dialog()?.hasAttribute('open')).toBe(false);
    confirmation.waiting.set(true);
    await settle();
    host.querySelector<HTMLButtonElement>('.dlg-a .btn:not(.solid)')?.click();
    await settle();
    expect(confirmation.dismissed).toBeGreaterThan(0);
    expect(dialog()?.hasAttribute('open')).toBe(false);
  });

  it('a company person is told to ask a password administrator; no password field', async () => {
    user.set({ ...amal, amr: ['external', 'mfa'] });
    confirmation.waiting.set(true);
    await settle();
    expect(host.querySelector('#confirm-identity-password')).toBeNull();
    expect(host.querySelector('.dlg-m')?.textContent).toContain('Your company signs you in');
  });
});
