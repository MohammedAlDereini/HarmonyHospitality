import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { AccessStore } from '@harmony/platform/access';
import { FailureNotice } from '@harmony/platform/errors';
import { SessionStore } from '@harmony/platform/session';
import { filter, skip } from 'rxjs';
import { BrandLockup } from './brand-lockup';

/** The navigation that brought the shell itself to life; focus moves only on the ones after it. */
const INITIAL_NAVIGATION = 1;
/** A heading reachable by script, never by the Tab key. */
const FOCUSABLE_BY_SCRIPT_ONLY = -1;

/**
 * The frame every signed-in screen lives in (architecture 8.3, 10, 14): the global bar, the strips for a gone
 * session, a BFF that does not answer and an unexpected failure, the skip link and the main landmark.
 */
@Component({
  selector: 'hr-shell',
  imports: [RouterOutlet, BrandLockup],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell {
  protected readonly session = inject(SessionStore);
  protected readonly notice = inject(FailureNotice);
  private readonly access = inject(AccessStore);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly injector = inject(Injector);
  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');
  private readonly leaveWarning = $localize`:@@ui.shell.leave-warning:The sign-in window could not open. Sign in on this page instead? Changes not saved will be lost.`;

  constructor() {
    void this.session.ready();
    // The menus and guards draw from the grants; read them once the frame is up.
    void this.access.ready();
    // Rule 15: after each navigation the main heading takes focus, so a screen reader starts at the new page.
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        skip(INITIAL_NAVIGATION),
        takeUntilDestroyed(),
      )
      .subscribe(() => {
        afterNextRender(
          () => {
            this.focusHeading();
          },
          { injector: this.injector },
        );
      });
  }

  protected skipToMain(event: Event): void {
    event.preventDefault();
    this.main().nativeElement.focus();
  }

  /** The separate window first; when the browser refuses it, a full-page sign-in only after the person agrees. */
  protected signInAgain(): void {
    if (this.session.signInAgain()) return;
    if (this.document.defaultView?.confirm(this.leaveWarning) === true) {
      this.session.signIn(this.router.url);
    }
  }

  protected tryAgain(): void {
    void this.session.refresh();
  }

  private focusHeading(): void {
    const main = this.main().nativeElement;
    const heading = main.querySelector('h1');
    if (heading === null) {
      main.focus();
      return;
    }
    heading.tabIndex = FOCUSABLE_BY_SCRIPT_ONLY;
    heading.focus();
  }
}
