import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HealthStore, SummariesStore } from '@harmony/platform-operations/data-access';
import {
  HealthEntry,
  HealthStatus,
  MonthCount,
  SYSTEM_ACTOR,
  TrailEvent,
  TrailTone,
  healthTone,
  trailTone,
  trailWording,
  utcDay,
  utcTime,
} from '@harmony/platform-operations/domain';
import { AccessStore, Permission } from '@harmony/platform/access';
import { SessionStore } from '@harmony/platform/session';
import { Icon } from '@harmony/ui/icons';
import { PageHead } from '@harmony/ui/shell';
import { AttentionStore } from '../attention-store';

/** The day in three parts, by the person's own clock. */
const NOON = 12;
const EVENING = 18;
/** The chart's drawing: twelve slots of this width, bars this wide, this much height for the bars. */
const SLOT_WIDTH = 50;
const BAR_WIDTH = 26;
const BAR_SPACE = 120;
const CHART_HEIGHT = 160;
const BASELINE = 140;
const LABEL_LINE = 156;
const VALUE_GAP = 6;

// Runs of whitespace (spaces, newlines) between the words of a name. Matches "Omar  Haddad" (→ two words); rejects
// "Omar" (nothing to split).
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=Omar%20%20Haddad
const WHITESPACE = /\s+/;

const statusWords: Readonly<Record<HealthStatus, string>> = {
  Healthy: $localize`:@@harmony-console.home.health.live:Live`,
  Degraded: $localize`:@@harmony-console.home.health.degraded:Degraded`,
  Unhealthy: $localize`:@@harmony-console.home.health.down:Down`,
  Unreachable: $localize`:@@harmony-console.home.health.unreachable:Not answering`,
};

/** One bar of the onboarding chart, placed on the SVG's grid. */
interface Bar {
  readonly month: string;
  readonly label: string;
  readonly count: number;
  readonly x: number;
  readonly y: number;
  readonly height: number;
  readonly centre: number;
  readonly current: boolean;
}

/** One line of the home's health card. */
interface HealthLine {
  readonly name: string;
  readonly note: string;
  readonly status: HealthStatus;
}

/**
 * The console's home (Figma C1): the greeting with the platform's numbers in one line, what needs a person now, four
 * numbers, the customers onboarded each month, the platform's health and the latest of the trail. Everything comes
 * from doors the console already reads; a door the person may not open leaves its card quiet.
 */
@Component({
  selector: 'hr-home',
  imports: [RouterLink, Icon, PageHead],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home {
  protected readonly attention = inject(AttentionStore);
  protected readonly summaries = inject(SummariesStore);
  protected readonly health = inject(HealthStore);
  private readonly session = inject(SessionStore);
  private readonly access = inject(AccessStore);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly canSeeTenants = computed(() => this.access.can(Permission.ViewTenants));
  protected readonly canSeeHealth = computed(() => this.access.can(Permission.ViewSettings));
  protected readonly tenants = this.summaries.tenants;
  protected readonly properties = this.summaries.properties;

  /** "Good afternoon, Omar": by the person's own clock, their first name. */
  protected readonly greeting = computed(() => {
    const hour = new Date().getHours();
    const name = (this.session.user()?.name ?? '').trim().split(WHITESPACE)[0] ?? '';
    if (hour < NOON) return $localize`:@@harmony-console.home.morning:Good morning, ${name}:name:`;
    if (hour < EVENING)
      return $localize`:@@harmony-console.home.afternoon:Good afternoon, ${name}:name:`;
    return $localize`:@@harmony-console.home.evening:Good evening, ${name}:name:`;
  });

  /** "Tue 28 Oct 2026 · 14:05 UTC". */
  protected readonly today = computed(() => {
    const now = new Date();
    const day = new Intl.DateTimeFormat(this.locale, {
      weekday: 'short',
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      timeZone: 'UTC',
    }).format(now);
    const time = new Intl.DateTimeFormat(this.locale, {
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
      timeZone: 'UTC',
    }).format(now);
    return `${day} · ${time} UTC`;
  });

  protected readonly bars = computed<readonly Bar[]>(() => {
    const months = this.tenants()?.onboardedByMonth ?? [];
    const top = Math.max(1, ...months.map((month) => month.count));
    return months.map((month, index) => {
      const height = Math.round((month.count / top) * BAR_SPACE);
      return {
        month: month.month,
        label: this.monthLabel(month),
        count: month.count,
        x: index * SLOT_WIDTH + (SLOT_WIDTH - BAR_WIDTH) / 2,
        y: BASELINE - height,
        height,
        centre: index * SLOT_WIDTH + SLOT_WIDTH / 2,
        current: index === months.length - 1,
      };
    });
  });
  protected readonly chartWidth = computed(() => this.bars().length * SLOT_WIDTH);
  protected readonly chartHeight = CHART_HEIGHT;
  protected readonly barWidth = BAR_WIDTH;
  protected readonly baseline = BASELINE;
  protected readonly labelLine = LABEL_LINE;
  protected readonly valueGap = VALUE_GAP;
  protected readonly onboardedInYear = computed(() =>
    (this.tenants()?.onboardedByMonth ?? []).reduce((sum, month) => sum + month.count, 0),
  );

  /** The health card: Identity itself, this host, then every check that is not healthy, then the healthy ones. */
  protected readonly healthLines = computed<readonly HealthLine[]>(() => {
    const report = this.health.report();
    if (report === null) return [];
    const identity: HealthLine = {
      name: $localize`:@@harmony-console.home.health.identity:Identity service`,
      note: $localize`:@@harmony-console.home.health.identity.note:${report.entries.length}:count: checks`,
      status: report.status,
    };
    const host: HealthLine = {
      name: $localize`:@@harmony-console.home.health.bff:Web BFF · Console BFF`,
      note: `${report.host.ring} · ${report.host.release}`,
      status: 'Healthy',
    };
    const byName = (entry: HealthEntry): HealthLine => ({
      name: entry.name,
      note: entry.description ?? '',
      status: entry.status,
    });
    const unwell = report.entries.filter((entry) => entry.status !== 'Healthy').map(byName);
    const well = report.entries.filter((entry) => entry.status === 'Healthy').map(byName);
    return [identity, host, ...unwell, ...well];
  });
  protected readonly healthAttention = computed(
    () => this.health.report()?.entries.filter((entry) => entry.status !== 'Healthy').length ?? 0,
  );

  constructor() {
    // Back on the home: the numbers and the trail are read again, so what changed elsewhere shows here.
    this.attention.reload();
  }

  protected refresh(): void {
    this.attention.reload();
  }

  protected tone(status: HealthStatus): 'ok' | 'warn' | 'stop' {
    return healthTone(status);
  }

  protected statusWord(status: HealthStatus): string {
    return statusWords[status];
  }

  protected time(row: TrailEvent): string {
    return utcTime(row.occurredAtUtc, this.locale);
  }

  protected day(row: TrailEvent): string {
    return utcDay(row.occurredAtUtc, this.locale, new Date());
  }

  protected wording(row: TrailEvent): string {
    return trailWording(row.action);
  }

  protected actor(row: TrailEvent): string {
    if (row.actorSubject === SYSTEM_ACTOR)
      return $localize`:@@platform-operations.trail.actor.system:system`;
    return row.actorDisplayName ?? row.actorSubject;
  }

  protected trailTone(row: TrailEvent): TrailTone {
    return trailTone(row.action);
  }

  private monthLabel(month: MonthCount): string {
    const [year, monthNumber] = month.month.split('-');
    return new Intl.DateTimeFormat(this.locale, { month: 'short', timeZone: 'UTC' }).format(
      new Date(`${year ?? ''}-${monthNumber ?? ''}-01T00:00:00Z`),
    );
  }
}
