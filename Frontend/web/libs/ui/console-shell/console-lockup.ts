import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The console lockup from the Figma (Logo/Console lockup on shell, 3:33): the Round door symbol, the HARMONY wordmark,
 * a divider and the word Console; the Arabic build names it in Arabic (3:46). Drawn in currentColor on the shell.
 */
@Component({
  selector: 'hr-console-lockup',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="lockup" role="img" [attr.aria-label]="label">
      <svg
        class="sym"
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 18.3333 22"
        aria-hidden="true"
        focusable="false"
      >
        <path
          fill="currentColor"
          fill-rule="evenodd"
          clip-rule="evenodd"
          d="M0 0H4.58333V22H0V0ZM13.75 0H18.3333V22H13.75V0ZM4.35417 5.5H13.9792V14.2083C13.9792 12.932 13.4721 11.7079 12.5696 10.8054C11.6671 9.90286 10.443 9.39583 9.16667 9.39583C7.89031 9.39583 6.66623 9.90286 5.76372 10.8054C4.8612 11.7079 4.35417 12.932 4.35417 14.2083V5.5Z"
        />
      </svg>
      @if (arabic) {
        <b class="ar" lang="ar">هارموني</b>
      } @else {
        <svg
          class="word"
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 132.404 17"
          aria-hidden="true"
          focusable="false"
        >
          <g fill="currentColor">
            <path
              d="M85.9808 0C88.2351 0 90.3972 0.895537 91.9913 2.4896C93.5852 4.08366 94.4808 6.24565 94.4808 8.5C94.4808 10.7543 93.5852 12.9163 91.9913 14.5104C90.3972 16.1045 88.2351 17 85.9808 17C83.7265 17 81.5644 16.1045 79.9703 14.5104C78.3764 12.9163 77.4808 10.7543 77.4808 8.5C77.4808 6.24565 78.3764 4.08366 79.9703 2.4896C81.5644 0.895537 83.7265 0 85.9808 0ZM85.9808 2.45192C84.3767 2.45192 82.8384 3.08911 81.7041 4.22334C80.5699 5.35758 79.9327 6.89595 79.9327 8.5C79.9327 10.104 80.5699 11.6424 81.7041 12.7767C82.8384 13.9109 84.3767 14.5481 85.9808 14.5481C87.5848 14.5481 89.1232 13.9109 90.2574 12.7767C91.3917 11.6424 92.0288 10.104 92.0288 8.5C92.0288 6.89595 91.3917 5.35758 90.2574 4.22334C89.1232 3.08911 87.5848 2.45192 85.9808 2.45192Z"
            />
            <path
              d="M2.45192 6.94712H10.2981V0.326923H12.75V16.6731H10.2981V9.39904H2.45192V16.6731H0V0.326923H2.45192V6.94712Z"
            />
            <path
              d="M31.2697 16.6731H28.6555L27.2946 12.9952H21.7438L20.383 16.6731H17.7688L23.8168 0.326923H25.2216L31.2697 16.6731ZM22.6512 10.5433H26.3873L24.5192 5.49448L22.6512 10.5433Z"
            />
            <path
              d="M43.8077 0.326923C45.1082 0.326923 46.3556 0.843628 47.2752 1.76328C48.1948 2.68293 48.7115 3.93019 48.7115 5.23077C48.7115 6.53135 48.1948 7.77861 47.2752 8.69826C46.6289 9.34456 45.8207 9.79139 44.946 10.0002L49.0832 16.6731H46.1983L42.1445 10.1346H39.3942V16.6731H36.9423V0.326923H43.8077ZM39.3942 7.68269H43.8077C44.4579 7.68269 45.0815 7.42434 45.5414 6.96451C46.0012 6.50469 46.2596 5.88107 46.2596 5.23077C46.2596 4.58047 46.0012 3.95685 45.5414 3.49703C45.0815 3.0372 44.4579 2.77885 43.8077 2.77885H39.3942V7.68269Z"
            />
            <path
              d="M63.75 12.9906L69.3564 0.326923H71.9231V16.6731H69.4712V6.12471L64.8014 16.6731H62.6986L58.0288 6.12471V16.6731H55.5769V0.326923H58.1436L63.75 12.9906Z"
            />
            <path
              d="M110.99 12.6407V0.326923H113.442V16.6731H110.741L102.49 4.35919V16.6731H100.038V0.326923H102.74L110.99 12.6407Z"
            />
            <path
              d="M124.885 7.2461L129.425 0.326923H132.358L126.111 9.84712V16.6731H123.659V9.84712L117.411 0.326923H120.344L124.885 7.2461Z"
            />
          </g>
        </svg>
      }
      @if (product() !== '') {
        <span class="div" aria-hidden="true"></span>
        <span class="prod">{{ product() }}</span>
      }
    </span>
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      line-height: 0;
    }
    .lockup {
      display: inline-flex;
      align-items: center;
      gap: 10px;
      color: var(--shell-ink);
    }
    .sym {
      width: 18.333px;
      height: 22px;
      flex: none;
    }
    .word {
      width: 132.404px;
      height: 17px;
      flex: none;
    }
    .ar {
      font: 700 19px/1 var(--ar);
      letter-spacing: 0;
    }
    .div {
      width: 1px;
      height: 18px;
      background: var(--shell-rule);
      flex: none;
    }
    .prod {
      font-size: 13px;
      line-height: 18px;
      color: var(--shell-dim);
      white-space: nowrap;
    }
    /* A phone keeps the symbol alone; the bar shows the page's title beside it (Responsive 22, M1). */
    @media (max-width: 720px) {
      .word,
      .ar,
      .div,
      .prod {
        display: none;
      }
    }
  `,
})
export class ConsoleLockup {
  /** The product word after the divider; the phone bar shows the page title instead and passes ''. */
  readonly product = input<string>($localize`:@@ui.console.lockup.product:Console`);
  protected readonly arabic = ($localize.locale ?? 'en').startsWith('ar');
  protected readonly label = $localize`:@@ui.console.lockup.label:Harmony console`;
}
