import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  InjectionToken,
  computed,
  inject,
  input,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { crumbsOf } from './crumbs';
import { PageActions } from './page-actions';

/**
 * Where a page's actions are drawn: false in the hotel app (the shell's context bar renders them, next to the
 * breadcrumb); true in the platform console (the page head renders them beside its title, as the Figma draws it).
 */
export const PAGE_ACTIONS_IN_HEAD = new InjectionToken<boolean>('PAGE_ACTIONS_IN_HEAD', {
  providedIn: 'root',
  factory: () => false,
});

/**
 * The head of a page: the title, one line under it, and, where the app says so, the trail of sections above it and the
 * page's own actions beside it (the templates offered with hrPageActions). The hotel app keeps its context bar; the
 * console has none, so here is where its actions live.
 */
@Component({
  selector: 'hr-page-head',
  imports: [NgTemplateOutlet, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ph' },
  template: `
    @if (inHead && trail().length > 0) {
      <nav class="ph-crumb" i18n-aria-label="@@ui.shell.breadcrumb" aria-label="Breadcrumb">
        @for (part of trail(); track $index) {
          <a [routerLink]="upOne($index)">{{ part }}</a
          ><span class="sl" aria-hidden="true">›</span>
        }
        <span aria-current="page">{{ title() }}</span>
      </nav>
    }
    <header class="page-head">
      <div class="ph-text">
        <div class="ph-before"><ng-content select="[slot=before-title]" /></div>
        <div class="ph-main">
          <div class="ph-title">
            <h1>{{ title() }}</h1>
            <ng-content select="[slot=after-title]" />
          </div>
          @if (lead() !== '') {
            <p class="lead">{{ lead() }}</p>
          }
          <ng-content select="[slot=lead]" />
        </div>
      </div>
      <ng-content select="[slot=aside]" />
      @if (inHead) {
        <div class="ph-acts">
          <ng-content select="[slot=actions]" />
          @if (actions.template(); as template) {
            <ng-container *ngTemplateOutlet="template" />
          }
        </div>
      }
    </header>
  `,
  styles: `
    /* The tile or avatar offered before the title stands beside the heading and its lead, both as tall as the two. */
    .ph-text {
      display: flex;
      align-items: flex-start;
      min-width: 0;
    }
    .ph-before {
      display: flex;
      align-items: center;
      flex: none;
    }
    .ph-before:empty {
      display: none;
    }
    .ph-main {
      flex: 1 1 auto;
      min-width: 0;
    }
    .ph-title {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
    }
    .ph-title h1 {
      margin: 0;
    }
  `,
})
export class PageHead {
  readonly title = input.required<string>();
  readonly lead = input('');
  protected readonly inHead = inject(PAGE_ACTIONS_IN_HEAD);
  protected readonly actions = inject(PageActions);
  private readonly router = inject(Router);

  /** The sections passed on the way to this page, from the route data; empty on a top page. */
  protected readonly crumbs = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => crumbsOf(this.router.routerState.snapshot.root)),
      startWith(crumbsOf(this.router.routerState.snapshot.root)),
    ),
    { requireSync: true },
  );

  /** The trail above the title; a page named after its own section (a reference table) does not repeat it. */
  protected readonly trail = computed(() => {
    const parts = this.crumbs().trail;
    const last = parts[parts.length - 1];
    return last === this.title() ? parts.slice(0, parts.length - 1) : parts;
  });

  /** The link of a crumb: the URL cut back to that many segments from the end (a detail page's list is one up). */
  protected upOne(index: number): string {
    const segments =
      this.router.url
        .split('?')[0]
        ?.split('/')
        .filter((s) => s !== '') ?? [];
    const keep = Math.max(0, segments.length - (this.trail().length - index));
    return '/' + segments.slice(0, keep).join('/');
  }
}
