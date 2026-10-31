import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ConsoleLockup } from './console-lockup';

@Component({
  selector: 'hr-lockup-host',
  imports: [ConsoleLockup],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hr-console-lockup [product]="product()" />`,
})
class LockupHost {
  readonly product = input('Console');
}

/** H, A, R, M, O, N, Y: one path each in the wordmark. */
const WORDMARK_LETTERS = 7;

describe('ConsoleLockup', () => {
  it('draws the symbol, the wordmark and the product word, named as one image for a screen reader', async () => {
    const fixture = TestBed.createComponent(LockupHost);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('.lockup')?.getAttribute('aria-label')).toBe('Harmony console');
    expect(page.querySelector('svg.sym path')).not.toBeNull();
    expect(page.querySelectorAll('svg.word path')).toHaveLength(WORDMARK_LETTERS);
    expect(page.querySelector('.prod')?.textContent).toBe('Console');
    expect(page.querySelector('.div')).not.toBeNull();
    expect(page.querySelector('.ar')).toBeNull();
  });

  it('leaves out the divider and the product word when the host passes none', async () => {
    const fixture = TestBed.createComponent(LockupHost);
    fixture.componentRef.setInput('product', '');
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('.prod')).toBeNull();
    expect(page.querySelector('.div')).toBeNull();
    expect(page.querySelector('svg.word')).not.toBeNull();
  });
});
