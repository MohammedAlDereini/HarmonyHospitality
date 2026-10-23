import { ChangeDetectionStrategy, Component } from '@angular/core';

/** The Harmony lockup from the prototype home(8): the symbol with the wordmark, or with the Arabic name. */
@Component({
  selector: 'hr-brand-lockup',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './brand-lockup.html',
  styleUrl: './brand-lockup.css',
})
export class BrandLockup {
  /** Each build is one language; the Arabic lockup belongs to the Arabic build. */
  protected readonly arabic = ($localize.locale ?? 'en').startsWith('ar');
}
