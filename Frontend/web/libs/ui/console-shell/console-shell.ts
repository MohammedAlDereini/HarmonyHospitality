import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AccessStore } from '@harmony/platform/access';
import { AppUpdate } from '@harmony/platform/app-update';
import { RUNTIME_CONFIG } from '@harmony/platform/config';
import { FailureNotice } from '@harmony/platform/errors';
import { FlagsStore } from '@harmony/platform/flags';
import { SessionStore } from '@harmony/platform/session';
import { initialsOf } from '@harmony/shared/domain';
import { Icon } from '@harmony/ui/icons';
import { ConfirmDialog, ToastsHost } from '@harmony/ui/overlays';
import { ConfirmIdentityDialog, LookStore, NavPanel, PopAt, crumbsOf } from '@harmony/ui/shell';
import { filter, map, skip, startWith } from 'rxjs';
import { ConsoleLockup } from './console-lockup';
import { CONSOLE_ATTENTION, CONSOLE_PLACES } from './console-places';

/** The navigation that brought the shell itself to life; focus moves only on the ones after it. */
const INITIAL_NAVIGATION = 1;
/** A heading reachable by script, never by the Tab key. */
const FOCUSABLE_BY_SCRIPT_ONLY = -1;
/** The one hint this device keeps about the rail: whether the person folded it (architecture 9: hints only). */
const RAIL_HINT = 'harmony.console.nav';
const FOLDED = 'collapsed';
/** Below this width the rail is folded whatever the person chose (Responsive 22: the tablet). */
const NARROW = '(max-width: 1100px)';

/** The device's storage, or null where the browser refuses it. */
const storageOf = (document: Document): Storage | null => {
  try {
    return document.defaultView?.localStorage ?? null;
  } catch {
    return null;
  }
};

/**
 * The platform console's frame, as the Figma draws it (Top bar 4:27, Side nav 4:68, Responsive 22): the 48 px bar
 * with the console lockup, the search that is the Ctrl K palette, the environment chip, the bell, the help and the
 * account; the 240 px side navigation in sections, foldable to a rail; the page. On a tablet the rail is folded, on
 * a phone the places move to a bottom bar. The strips for a gone session, a BFF that does not answer and an
 * unexpected failure are the same as the hotel app's; menus are native popovers.
 */
@Component({
  selector: 'hr-console-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    Icon,
    NavPanel,
    PopAt,
    ConfirmDialog,
    ToastsHost,
    ConfirmIdentityDialog,
    ConsoleLockup,
  ],
  host: { '(document:keydown)': 'onKey($event)' },
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './console-shell.html',
  styleUrl: './console-shell.css',
})
export class ConsoleShell {
  protected readonly session = inject(SessionStore);
  protected readonly notice = inject(FailureNotice);
  protected readonly look = inject(LookStore);
  protected readonly update = inject(AppUpdate);
  protected readonly places = inject(CONSOLE_PLACES);
  protected readonly attention = inject(CONSOLE_ATTENTION);
  private readonly flags = inject(FlagsStore);
  private readonly config = inject(RUNTIME_CONFIG);
  private readonly access = inject(AccessStore);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);
  private readonly storage = storageOf(this.document);
  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');
  private readonly navPanel = viewChild.required(NavPanel);
  private readonly leaveDialog = viewChild.required<ConfirmDialog>('leave');
  private readonly meButton = viewChild<ElementRef<HTMLElement>>('meButton');
  private readonly bellButton = viewChild<ElementRef<HTMLElement>>('bellButton');
  private readonly helpButton = viewChild<ElementRef<HTMLElement>>('helpButton');
  private readonly locale = $localize.locale ?? 'en';

  protected readonly arabic = this.locale.startsWith('ar');
  protected readonly meAnchor = computed(() => this.meButton()?.nativeElement);
  protected readonly bellAnchor = computed(() => this.bellButton()?.nativeElement);
  protected readonly helpAnchor = computed(() => this.helpButton()?.nativeElement);
  protected readonly initials = computed(() => initialsOf(this.session.user()?.name ?? ''));
  protected readonly release = this.config.release;
  /** The rail folded to icons, as this device last left it. */
  protected readonly collapsed = signal(this.storage?.getItem(RAIL_HINT) === FOLDED);
  /** The window is a tablet's: the rail folds on its own and the fold button goes. */
  private readonly narrow = signal(false);
  protected readonly folded = computed(() => this.collapsed() || this.narrow());
  protected readonly waiting = computed(() => this.attention().length);
  protected readonly collapseLabel = $localize`:@@ui.console.nav.collapse:Collapse`;
  protected readonly expandLabel = $localize`:@@ui.console.nav.expand:Expand`;

  /** "Production · Everyone ring": the BFF's environment and, once the flags answered, the ring it serves. */
  protected readonly environment = computed(() => {
    const ring = this.flags.ring();
    const environment = this.config.environment;
    return ring === null
      ? environment
      : $localize`:@@ui.console.environment.ring:${environment}:environment: · ${ring}:ring: ring`;
  });

  /** The page's title for the phone bar; the console's name when the route has none. */
  protected readonly pageTitle = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => crumbsOf(this.router.routerState.snapshot.root).page),
      startWith(crumbsOf(this.router.routerState.snapshot.root).page),
    ),
    { requireSync: true },
  );

  constructor() {
    void this.session.ready();
    // The rail and the guards draw from the grants; read them once the frame is up.
    void this.access.ready();
    this.followWidth();
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

  /** The BFF serves the other language's build at the same URL (B2); a link, so the page is rebuilt. */
  protected languageLink(language: string): string {
    return `?${new URLSearchParams({ lang: language }).toString()}`;
  }

  protected skipToMain(event: Event): void {
    event.preventDefault();
    this.main().nativeElement.focus();
  }

  /** Folds the rail to icons or opens it again; the choice stays on this device. */
  protected toggleRail(): void {
    const folded = !this.collapsed();
    this.collapsed.set(folded);
    try {
      if (folded) this.storage?.setItem(RAIL_HINT, FOLDED);
      else this.storage?.removeItem(RAIL_HINT);
    } catch {
      // A hint only; the rail works without it.
    }
  }

  /** The bar's search and the phone's menu both open the places panel, the console's command palette. */
  protected openPalette(): void {
    this.navPanel().open();
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

  /** Folds the rail below the tablet width and opens it again above; nothing where the window cannot say. */
  private followWidth(): void {
    const view = this.document.defaultView;
    if (view === null || typeof view.matchMedia !== 'function') return;
    const query = view.matchMedia(NARROW);
    this.narrow.set(query.matches);
    const onChange = (event: MediaQueryListEvent): void => {
      this.narrow.set(event.matches);
    };
    query.addEventListener('change', onChange);
    this.destroyRef.onDestroy(() => {
      query.removeEventListener('change', onChange);
    });
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
