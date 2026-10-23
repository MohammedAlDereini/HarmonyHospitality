import { TestBed } from '@angular/core/testing';
import { SessionChannel, SessionMessage } from './session-channel';
import { SignedInPage } from './signed-in-page';

describe('SignedInPage', () => {
  it('tells the original tab, then closes its window', async () => {
    const posted: SessionMessage[] = [];
    const close = vi.spyOn(window, 'close').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [
        {
          provide: SessionChannel,
          useValue: {
            post: (message: SessionMessage) => {
              posted.push(message);
            },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(SignedInPage);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(posted).toEqual([{ type: 'signed-in' }]);
    expect(close).toHaveBeenCalledOnce();
    close.mockRestore();
  });
});
