import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AccessStore } from '@harmony/platform/access';
import { AppUpdate } from '@harmony/platform/app-update';
import {
  CurrentProperty,
  MyProperties,
  PropertySummary,
  propertyName,
} from '@harmony/platform/context';
import { FailureNotice } from '@harmony/platform/errors';
import { SessionStore } from '@harmony/platform/session';
import { initialsOf } from '@harmony/shared/domain';
import { Icon } from '@harmony/ui/icons';
import { ConfirmDialog, ToastsHost } from '@harmony/ui/overlays';
import { filter, map, skip, startWith } from 'rxjs';
import { BrandLockup } from './brand-lockup';
import { ConfirmIdentityDialog } from './confirm-identity-dialog';
import { crumbsOf } from './crumbs';
import { LookStore } from './look-store';
import { NavPanel } from './nav-panel';
import { PageActions } from './page-actions';
import { SHELL_PLACES } from './places';
import { PopAt } from './pop-at.directive';

/** The navigation that brought the shell itself to life; focus moves only on the ones after it. */
const INITIAL_NAVIGATION = 1;
/** A heading reachable by script, never by the Tab key. */
const FOCUSABLE_BY_SCRIPT_ONLY = -1;

/**
 * The frame every signed-in screen lives in, as the prototype home(8) drew it (architecture 8.3, 10, 14): the 44 px
 * global bar with the menu, the lockup, the operating scope, the search and the account; the dock of workspaces; the
 * 40 px context bar with the breadcrumb and the page's own actions; the strips for a gone session, a BFF that does
 * not answer and an unexpected failure; the skip link and the main landmark. Menus are native popovers placed under
 * their buttons.
 */
@Component({
  selector: 'hr-shell',
  imports: [
    NgTemplateOutlet,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    BrandLockup,
    Icon,
    NavPanel,
    PopAt,
    ConfirmDialog,
    ToastsHost,
    ConfirmIdentityDialog,
  ],
  // Ctrl K (Cmd K on a Mac) opens the places panel as the command palette (architecture 8.3).
  host: { '(document:keydown)': 'onKey($event)' },
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.css',
})
export class Shell {
  protected readonly session = inject(SessionStore);
  protected readonly notice = inject(FailureNotice);
  protected readonly properties = inject(MyProperties);
  protected readonly current = inject(CurrentProperty);
  protected readonly look = inject(LookStore);
  protected readonly actions = inject(PageActions);
  /** Whether this build may keep running (D38): below the floor the strip asks for a reload. */
  protected readonly update = inject(AppUpdate);
  /** The app's places: what the nav panel lists and the dock shows; whether the bar carries a property scope. */
  protected readonly places = inject(SHELL_PLACES);
  private readonly access = inject(AccessStore);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly injector = inject(Injector);
  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');
  private readonly navPanel = viewChild.required(NavPanel);
  private readonly leaveDialog = viewChild.required<ConfirmDialog>('leave');
  private readonly propButton = viewChild<ElementRef<HTMLElement>>('propButton');
  private readonly meButton = viewChild<ElementRef<HTMLElement>>('meButton');
  private readonly locale = $localize.locale ?? 'en';

  protected readonly arabic = this.locale.startsWith('ar');
  protected readonly propAnchor = computed(() => this.propButton()?.nativeElement);
  protected readonly meAnchor = computed(() => this.meButton()?.nativeElement);
  protected readonly initials = computed(() => initialsOf(this.session.user()?.name ?? ''));

  /** The property's name when the URL is inside one; otherwise the invitation to choose. */
  protected readonly propertyLabel = computed(() => {
    const code = this.current.code();
    const property = this.properties.list().find((p) => p.code === code);
    if (property !== undefined) return propertyName(property, this.locale);
    return code ?? $localize`:@@ui.shell.choose-property:Choose a property`;
  });

  protected readonly inProperty = this.places.scope === 'property';

  /** Where am I (the context bar): sections from route data, the page from the route title. */
  protected readonly crumbs = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => crumbsOf(this.router.routerState.snapshot.root)),
      startWith(crumbsOf(this.router.routerState.snapshot.root)),
    ),
    { requireSync: true },
  );

  constructor() {
    void this.session.ready();
    // The menus, the dock and the guards draw from the grants, and inside a hotel from the hotels; read them once the frame is up.
    void this.access.ready();
    if (this.inProperty) void this.properties.ready();
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

  protected name(property: PropertySummary): string {
    return propertyName(property, this.locale);
  }

  /** The BFF serves the other language's build at the same URL (B2); a link, so the page is rebuilt. */
  protected languageLink(language: string): string {
    return `?${new URLSearchParams({ lang: language }).toString()}`;
  }

  protected skipToMain(event: Event): void {
    event.preventDefault();
    this.main().nativeElement.focus();
  }

  /** The separate window first; when the browser refuses it, a full-page sign-in only after the person agrees. */
  protected async signInAgain(): Promise<void> {
    if (this.session.signInAgain()) return;
    if (await this.leaveDialog().ask()) this.session.signIn(this.router.url);
  }

  /** Ctrl K or Cmd K, nothing else held: the places panel opens with its search focused. */
  protected onKey(event: KeyboardEvent): void {
    if (!(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey) return;
    if (event.key.toLowerCase() !== 'k') return;
    event.preventDefault();
    this.navPanel().open();
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
