import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** What one trail row needs to be drawn here: the page reads the rows and hands them over. */
export interface TrailCardRow {
  readonly id: string;
  readonly action: string;
  readonly detail: string | null;
  readonly actorSubject: string;
  readonly actorDisplayName: string | null;
  readonly occurredAtUtc: string;
}

/** Actions that read as a refusal or a loss; their dot is red, the rest neutral. */
const stopWords: readonly string[] = [
  'refused',
  'failed',
  'suspended',
  'terminated',
  'above-licence',
  'locked',
];
const DAY_MS = 86_400_000;
const TWO_DAYS_MS = 172_800_000;
const timeFormat: Intl.DateTimeFormatOptions = {
  hour: '2-digit',
  hourCycle: 'h23',
  minute: '2-digit',
  timeZone: 'UTC',
};
const dayFormat: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', timeZone: 'UTC' };

/**
 * The trail card of a customer's or a hotel's page (platform console design, C3, C16): the newest events, each as its
 * code and detail, then who and when, in UTC. The page owns the trail store and passes the rows; "All events" opens
 * the Audit log narrowed the same way.
 */
@Component({
  selector: 'hr-trail-card',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'c-card' },
  template: `
    <div class="c-card-h">
      <h3>{{ title() }}</h3>
      <span class="r">
        <a
          [routerLink]="['/trail']"
          [queryParams]="allParams()"
          i18n="@@platform-admin.trail-card.all"
          >All events</a
        >
      </span>
    </div>
    <div class="c-card-b">
      @if (problem(); as problem) {
        <p class="note" role="alert">{{ problem }}</p>
      } @else if (rows().length === 0) {
        <p class="note" i18n="@@platform-admin.trail-card.none">Nothing in the trail yet.</p>
      } @else {
        <ul class="events">
          @for (row of rows(); track row.id) {
            <li>
              <span class="dot" [attr.data-s]="tone(row)" aria-hidden="true"></span>
              <span class="text">
                <span class="nm" dir="ltr"
                  >{{ row.action }}
                  @if (row.detail) {
                    <span class="muted">
                      · <bdi>{{ row.detail }}</bdi></span
                    >
                  }
                </span>
                <span class="sub"
                  ><bdi>{{ actor(row) }}</bdi> · {{ when(row) }}</span
                >
              </span>
            </li>
          }
        </ul>
      }
    </div>
  `,
  styles: `
    .events {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: var(--s3);
    }
    .events li {
      display: flex;
      gap: var(--s3);
      align-items: flex-start;
    }
    .dot {
      flex: none;
      width: 8px;
      height: 8px;
      margin-top: 5px;
      border-radius: 50%;
      background: var(--dot-neutral);
    }
    .dot[data-s='stop'] {
      background: var(--stop);
    }
    .text {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .nm {
      font-size: 13px;
      line-height: 18px;
      font-weight: 600;
      color: var(--ink);
      overflow-wrap: anywhere;
    }
    .nm .muted {
      font-weight: 400;
      color: var(--ink-2);
    }
    .sub {
      font-size: 12px;
      line-height: 16px;
      color: var(--ink-3);
    }
    .note {
      margin: 0;
      font-size: 13px;
      color: var(--ink-3);
    }
  `,
})
export class TrailCard {
  readonly title = input.required<string>();
  readonly rows = input.required<readonly TrailCardRow[]>();
  readonly problem = input<string | null>(null);
  /** The query the Audit log opens with: a tenant, a search. */
  readonly allParams = input<Readonly<Record<string, string>>>({});

  private readonly locale = $localize.locale ?? 'en';
  private readonly times = new Intl.DateTimeFormat(this.locale, timeFormat);
  private readonly days = new Intl.DateTimeFormat(this.locale, dayFormat);

  protected tone(row: TrailCardRow): 'stop' | null {
    return stopWords.some((word) => row.action.includes(word)) ? 'stop' : null;
  }

  protected actor(row: TrailCardRow): string {
    if (row.actorSubject === 'system') return $localize`:@@platform-admin.trail-card.system:system`;
    return row.actorDisplayName ?? row.actorSubject;
  }

  /** "Today 11:52", "Yesterday 16:40", "21 Oct 09:12", in UTC. */
  protected when(row: TrailCardRow): string {
    const at = new Date(row.occurredAtUtc);
    const time = this.times.format(at);
    const age = Date.now() - at.getTime();
    if (age < DAY_MS && at.getUTCDate() === new Date().getUTCDate())
      return $localize`:@@platform-admin.trail-card.today:Today ${time}:time:`;
    if (age < TWO_DAYS_MS)
      return $localize`:@@platform-admin.trail-card.yesterday:Yesterday ${time}:time:`;
    return `${this.days.format(at)} ${time}`;
  }
}
