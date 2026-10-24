import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { IconName, icons } from './icon-set';

/** <hr-icon name="home" size="18" />: one icon from the prototype's line, in currentColor, decorative by default. */
@Component({
  selector: 'hr-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: inline-flex; line-height: 0; }',
  template: `
    <svg
      class="ic"
      [class.ic-dir]="mirrored()"
      [attr.width]="size()"
      [attr.height]="size()"
      [attr.viewBox]="drawing().viewBox"
      fill="none"
      stroke="currentColor"
      [attr.stroke-width]="drawing().strokeWidth"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      @for (shape of drawing().shapes; track $index) {
        @if (shape.kind === 'path') {
          <path [attr.d]="shape.d" />
        } @else if (shape.kind === 'circle') {
          <circle
            [attr.cx]="shape.cx"
            [attr.cy]="shape.cy"
            [attr.r]="shape.r"
            [attr.fill]="shape.filled ? 'currentColor' : null"
            [attr.stroke]="shape.filled ? 'none' : null"
          />
        } @else {
          <rect
            [attr.x]="shape.x"
            [attr.y]="shape.y"
            [attr.width]="shape.width"
            [attr.height]="shape.height"
            [attr.rx]="shape.rx"
          />
        }
      }
    </svg>
  `,
})
export class Icon {
  readonly name = input.required<IconName>();
  /** CSS pixels, as the prototype sized them: 18 in the dock, 13 to 14 beside text. */
  readonly size = input('16');
  /** A direction icon (an arrow) flips in a right-to-left page. */
  readonly mirrored = input(false);

  protected readonly drawing = computed(() => icons[this.name()]);
}
