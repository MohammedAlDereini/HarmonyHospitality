import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_RETRY_DELAYS, provideHarmonyHttp } from '@harmony/platform/http';
import { ForgotPasswordPage, forgotPasswordPath } from './forgot-password-page';
import { SessionStore } from './session-store';

const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));
const BAD_REQUEST = 400;

describe('ForgotPasswordPage', () => {
  let http: HttpTestingController;
  const signedInTo: string[] = [];

  const render = () => {
    TestBed.configureTestingModule({
      providers: [
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
    const fixture = TestBed.createComponent(ForgotPasswordPage);
    fixture.detectChanges();
    return { fixture, page: fixture.nativeElement as HTMLElement };
  };

  const typeEmail = (page: HTMLElement, value: string): void => {
    const box = page.querySelector<HTMLInputElement>('#forgot-email');
    if (box === null) throw new Error('no e-mail box');
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

  it('asks for a plausible address before it calls the door', async () => {
    const { fixture, page } = render();
    submit(page);
    await tick();
    fixture.detectChanges();
    expect(page.querySelector('.field-error')?.textContent).toContain('Give your work e-mail');
    typeEmail(page, 'lina@ritz');
    submit(page);
    await tick();
    fixture.detectChanges();
    expect(page.querySelector('.field-error')?.textContent).toContain('does not look like');
    http.expectNone(forgotPasswordPath);
  });

  it('posts the address with an intent key and then says to check the inbox, whatever the address', async () => {
    const { fixture, page } = render();
    typeEmail(page, 'lina@ritz.test');
    submit(page);
    await tick();
    const request = http.expectOne(forgotPasswordPath);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'lina@ritz.test' });
    expect(request.request.headers.has('Idempotency-Key')).toBe(true);
    request.flush({ data: 1 });
    await tick();
    fixture.detectChanges();
    expect(page.querySelector('h1')?.textContent).toContain('Check your inbox');
    page.querySelector('button')?.click();
    expect(signedInTo).toEqual(['/']);
  });

  it('words a refusal under the field and keeps the form', async () => {
    const { fixture, page } = render();
    typeEmail(page, 'lina@ritz.test');
    submit(page);
    await tick();
    http
      .expectOne(forgotPasswordPath)
      .flush(
        { data: null, errors: [{ code: '10517' }] },
        { status: BAD_REQUEST, statusText: 'Bad Request' },
      );
    await tick();
    fixture.detectChanges();
    expect(page.querySelector('form')).not.toBeNull();
    expect(page.querySelector('.field-error')?.textContent).toContain('email address');
  });
});
