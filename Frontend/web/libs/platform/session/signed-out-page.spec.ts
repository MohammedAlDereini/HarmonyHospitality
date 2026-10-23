import { TestBed } from '@angular/core/testing';
import { SessionStore } from './session-store';
import { SignedOutPage } from './signed-out-page';

describe('SignedOutPage', () => {
  it('offers one way back in: the sign-in door, home as the return path', () => {
    const signedInTo: string[] = [];
    TestBed.configureTestingModule({
      providers: [
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
    const fixture = TestBed.createComponent(SignedOutPage);
    fixture.detectChanges();
    const page: HTMLElement = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('h1')?.textContent).toContain('signed out');
    page.querySelector('button')?.click();
    expect(signedInTo).toEqual(['/']);
  });
});
