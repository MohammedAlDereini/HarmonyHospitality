import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_RETRY_DELAYS, provideHarmonyHttp } from '@harmony/platform/http';
import { SetPasswordMode, SetPasswordPage, resetPasswordPath } from './set-password-page';
import { SessionStore } from './session-store';

const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));
const STRONG = 'Orchard-Chosen-2026';
const ACCEPTED = 202;

describe('SetPasswordPage', () => {
  let http: HttpTestingController;
  const signedInTo: string[] = [];

  const render = (inputs: { email?: string; token?: string; mode?: SetPasswordMode }) => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [] },
        {
          provide: SessionStore,
          useValue: {
            signIn: (returnUrl: string) => {
              signedInTo.push(returnUrl);
            },
          },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(SetPasswordPage);
    fixture.componentRef.setInput('email', inputs.email ?? '');
    fixture.componentRef.setInput('token', inputs.token ?? '');
    fixture.componentRef.setInput('mode', inputs.mode ?? 'reset');
    fixture.detectChanges();
    return { fixture, page: fixture.nativeElement as HTMLElement };
  };

  const type = (page: HTMLElement, id: string, value: string): void => {
    const box = page.querySelector<HTMLInputElement>(`#${id}`);
    if (box === null) throw new Error(`no ${id}`);
    box.value = value;
    box.dispatchEvent(new Event('input'));
  };

  const submit = (page: HTMLElement): void => {
    page.querySelector('form')?.dispatchEvent(new Event('submit', { cancelable: true }));
  };

  afterEach(() => {
    http.verify();
    signedInTo.length = 0;
  });

  it('refuses a link without its address or token and offers a new one', () => {
    const { page } = render({ email: 'lina@ritz.test' });
    expect(page.querySelector('h1')?.textContent).toContain('incomplete');
    expect(page.querySelector('form')).toBeNull();
    expect(page.querySelector('a')?.getAttribute('href')).toBe('/forgot-password');
  });

  it('names the invitation and the reset differently', () => {
    const invitation = render({ email: 'lina@ritz.test', token: 't', mode: 'set' });
    expect(invitation.page.querySelector('h1')?.textContent).toContain('Choose your password');
    TestBed.resetTestingModule();
    const reset = render({ email: 'lina@ritz.test', token: 't', mode: 'reset' });
    expect(reset.page.querySelector('h1')?.textContent).toContain('Choose a new password');
  });

  it("mirrors Identity's rule and the repeat before it calls the door", async () => {
    const { fixture, page } = render({ email: 'lina@ritz.test', token: 'tok' });
    type(page, 'set-password', 'short');
    type(page, 'set-password-repeat', 'short');
    submit(page);
    await tick();
    fixture.detectChanges();
    expect(page.querySelector('.field-error')?.textContent).toContain('12 characters');
    type(page, 'set-password', STRONG);
    type(page, 'set-password-repeat', `${STRONG}x`);
    submit(page);
    await tick();
    fixture.detectChanges();
    expect(page.querySelector('.field-error')?.textContent).toContain('differ');
    http.expectNone(resetPasswordPath);
  });

  it('posts e-mail, token and the new password once, then offers to sign in', async () => {
    const { fixture, page } = render({ email: 'lina@ritz.test', token: 'tok' });
    type(page, 'set-password', STRONG);
    type(page, 'set-password-repeat', STRONG);
    submit(page);
    await tick();
    const request = http.expectOne(resetPasswordPath);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'lina@ritz.test',
      token: 'tok',
      newPassword: STRONG,
    });
    expect(request.request.headers.has('Idempotency-Key')).toBe(true);
    request.flush({ data: 1 });
    await tick();
    fixture.detectChanges();
    expect(page.querySelector('h1')?.textContent).toContain('Your password is set');
    page.querySelector('button')?.click();
    expect(signedInTo).toEqual(['/']);
  });

  it('words a used link in its own terms', async () => {
    const { fixture, page } = render({ email: 'lina@ritz.test', token: 'tok' });
    type(page, 'set-password', STRONG);
    type(page, 'set-password-repeat', STRONG);
    submit(page);
    await tick();
    http
      .expectOne(resetPasswordPath)
      .flush(
        { data: null, errors: [{ code: '00053' }] },
        { status: ACCEPTED, statusText: 'Accepted' },
      );
    await tick();
    fixture.detectChanges();
    expect(page.querySelector('.field-error')?.textContent).toContain(
      'already used or has expired',
    );
  });
});
