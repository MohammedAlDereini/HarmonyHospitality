import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { HealthStore } from '@harmony/platform-operations/data-access';
import {
  HealthEntry,
  HealthStatus,
  durationMs,
  healthTone,
  utcInstant,
} from '@harmony/platform-operations/domain';
import { FlagsStore } from '@harmony/platform/flags';
import { Icon } from '@harmony/ui/icons';
import { PageHead } from '@harmony/ui/shell';

/** How often the page reads the health again while live updates are on. */
const LIVE_EVERY_MS = 60_000;

/** A service on the map and how it stands today. */
type ServiceState = 'live' | 'attention' | 'down' | 'unreachable' | 'next' | 'planned';
type ServiceTone = 'ok' | 'warn' | 'stop' | 'info' | 'neutral';

interface ServiceCard {
  /** The map's number as text; the two hosts beside Identity have none. */
  readonly number: string | null;
  readonly name: string;
  readonly scope: string;
  readonly state: ServiceState;
  readonly note: string;
}

// Which checks speak for the infrastructure: the database, the cache, the broker, the mail relay. Matches
// "Identity database", "Redis", "RabbitMQ queue", "SMTP relay"; rejects "Self" and "Outbox".
// Tester: https://regex101.com/?regex=sql%7Cdatabase%7Credis%7Crabbit%7Csmtp%7Cmail&flags=i&testString=Identity%20database
const infrastructure = /sql|database|redis|rabbit|smtp|mail/i;
// Which checks speak for messaging: the outbox and inbox, the broker, the cache the bus publishes to. Matches "Outbox",
// "RabbitMQ"; rejects "Self".
// Tester: https://regex101.com/?regex=outbox%7Cinbox%7Crabbit%7Cevent%7Credis&flags=i&testString=Outbox
const messaging = /outbox|inbox|rabbit|event|redis/i;

/** The status words from the worst to the best: the first one present wins. */
const SEVERITY: readonly HealthStatus[] = ['Unreachable', 'Unhealthy', 'Degraded', 'Healthy'];

const worst = (statuses: readonly HealthStatus[]): HealthStatus =>
  SEVERITY.find((status) => statuses.includes(status)) ?? 'Healthy';

const states: Readonly<Record<HealthStatus, ServiceState>> = {
  Healthy: 'live',
  Degraded: 'attention',
  Unhealthy: 'down',
  Unreachable: 'unreachable',
};

const stateOf = (status: HealthStatus): ServiceState => states[status];

const statusWords: Readonly<Record<HealthStatus, string>> = {
  Healthy: $localize`:@@platform-operations.health.status.healthy:Healthy`,
  Degraded: $localize`:@@platform-operations.health.status.degraded:Degraded`,
  Unhealthy: $localize`:@@platform-operations.health.status.unhealthy:Unhealthy`,
  Unreachable: $localize`:@@platform-operations.health.status.unreachable:Not answering`,
};

const stateWords: Readonly<Record<ServiceState, string>> = {
  live: $localize`:@@platform-operations.health.state.live:Live`,
  attention: $localize`:@@platform-operations.health.state.attention:Attention`,
  down: $localize`:@@platform-operations.health.state.down:Down`,
  unreachable: $localize`:@@platform-operations.health.state.unreachable:Not answering`,
  next: $localize`:@@platform-operations.health.state.next:Next`,
  planned: $localize`:@@platform-operations.health.state.planned:Planned`,
};

const stateTones: Readonly<Record<ServiceState, ServiceTone>> = {
  live: 'ok',
  attention: 'warn',
  down: 'stop',
  unreachable: 'stop',
  next: 'info',
  planned: 'neutral',
};

/** The services still to come, in the map's build order (docs/services-map.html); the number is the map's. */
const PLANNED: readonly Omit<ServiceCard, 'state' | 'note'>[] = [
  {
    number: '2',
    name: $localize`:@@platform-operations.health.service.property:Property`,
    scope: $localize`:@@platform-operations.health.service.property.scope:hotel profile, business date, floors, room types, rooms`,
  },
  {
    number: '4',
    name: $localize`:@@platform-operations.health.service.guests:Guests & Loyalty`,
    scope: $localize`:@@platform-operations.health.service.guests.scope:chain-wide profiles, consent, merge, loyalty`,
  },
  {
    number: '3',
    name: $localize`:@@platform-operations.health.service.rates:Rates & Availability`,
    scope: $localize`:@@platform-operations.health.service.rates.scope:rate plans, restrictions, availability`,
  },
  {
    number: '5',
    name: $localize`:@@platform-operations.health.service.reservations:Reservations & Front Desk`,
    scope: $localize`:@@platform-operations.health.service.reservations.scope:bookings, arrivals, in-house, departures`,
  },
  {
    number: '6',
    name: $localize`:@@platform-operations.health.service.finance:Finance`,
    scope: $localize`:@@platform-operations.health.service.finance.scope:folios, taxes, end of day`,
  },
  {
    number: '7',
    name: $localize`:@@platform-operations.health.service.payments:Payments`,
    scope: $localize`:@@platform-operations.health.service.payments.scope:cards and terminals, its own PCI boundary`,
  },
  {
    number: '8',
    name: $localize`:@@platform-operations.health.service.housekeeping:Housekeeping & Maintenance`,
    scope: $localize`:@@platform-operations.health.service.housekeeping.scope:room status, tasks, out of order`,
  },
  {
    number: '13',
    name: $localize`:@@platform-operations.health.service.integrations:Integrations & Devices`,
    scope: $localize`:@@platform-operations.health.service.integrations.scope:key cards, telephony, fiscal printers`,
  },
  {
    number: '10',
    name: $localize`:@@platform-operations.health.service.distribution:Distribution`,
    scope: $localize`:@@platform-operations.health.service.distribution.scope:channels and online travel agents`,
  },
  {
    number: '11',
    name: $localize`:@@platform-operations.health.service.engagement:Guest Engagement`,
    scope: $localize`:@@platform-operations.health.service.engagement.scope:messages, mail, surveys`,
  },
  {
    number: '14',
    name: $localize`:@@platform-operations.health.service.reports:Reports`,
    scope: $localize`:@@platform-operations.health.service.reports.scope:reporting across the services`,
  },
  {
    number: '9',
    name: $localize`:@@platform-operations.health.service.sales:Sales, Events & Experiences`,
    scope: $localize`:@@platform-operations.health.service.sales.scope:groups, events, spa`,
  },
  {
    number: '12',
    name: $localize`:@@platform-operations.health.service.pos:POS`,
    scope: $localize`:@@platform-operations.health.service.pos.scope:tills and outlets`,
  },
];

/** The next service on the map stands out from the rest of the plan. */
const NEXT_SERVICE = '2';

/**
 * Platform health (Figma C10): the services map with what runs and what is planned, Identity's health checks by name
 * as the BFF reads them inside the network, the messaging checks on their own, and this deployment's ring, release and
 * flags. Live updates read again every minute and can be paused (WCAG 2.2.2).
 */
@Component({
  selector: 'hr-health-page',
  imports: [Icon, PageHead],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './health-page.html',
  styleUrl: './health-page.css',
})
export class HealthPage {
  protected readonly health = inject(HealthStore);
  protected readonly flags = inject(FlagsStore);
  private readonly document = inject(DOCUMENT);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly live = signal(true);
  protected readonly report = this.health.report;
  protected readonly entries = computed<readonly HealthEntry[]>(() => this.report()?.entries ?? []);
  protected readonly attention = computed(() =>
    this.entries().filter((entry) => entry.status !== 'Healthy'),
  );
  protected readonly messagingChecks = computed(() =>
    this.entries().filter((entry) => messaging.test(entry.name)),
  );
  protected readonly flagList = computed(() =>
    Object.entries(this.flags.flags()).map(([name, on]) => ({ name, on })),
  );
  protected readonly checkedAt = computed(() => {
    const report = this.report();
    return report === null ? '' : utcInstant(report.checkedAtUtc, this.locale);
  });
  /** How many of the map's cards stand for something running today: Identity and the two hosts and the infrastructure. */
  protected readonly liveCount = computed(
    () => this.services().filter((service) => service.state === 'live').length,
  );

  /** The map: what is built and how it stands, then the services still to come, in the build order of the map. */
  protected readonly services = computed<readonly ServiceCard[]>(() => {
    const report = this.report();
    const bffUp = this.health.problem() === null;
    const identity: HealthStatus = report?.status ?? (bffUp ? 'Healthy' : 'Unreachable');
    const infra = this.entries().filter((entry) => infrastructure.test(entry.name));
    const host = report?.host ?? null;
    const built: ServiceCard[] = [
      {
        number: '1',
        name: $localize`:@@platform-operations.health.service.identity:Identity`,
        scope: $localize`:@@platform-operations.health.service.identity.scope:tenants, people, roles, sign-in`,
        state: stateOf(identity),
        note:
          report === null
            ? ''
            : $localize`:@@platform-operations.health.service.identity.note:${report.entries.length}:count: checks · ${report.totalDuration ?? ''}:duration:`,
      },
      {
        number: null,
        name: $localize`:@@platform-operations.health.service.bff:Web BFF · Console BFF`,
        scope: $localize`:@@platform-operations.health.service.bff.scope:two hosts, own cookies and key rings`,
        state: bffUp ? 'live' : 'down',
        note: host === null ? '' : `${host.ring} · ${host.release} · ${host.environment}`,
      },
      {
        number: null,
        name: $localize`:@@platform-operations.health.service.infrastructure:Infrastructure`,
        scope: $localize`:@@platform-operations.health.service.infrastructure.scope:SQL Server · Redis · RabbitMQ · SMTP relay`,
        state:
          infra.length === 0
            ? stateOf(identity)
            : stateOf(worst(infra.map((entry) => entry.status))),
        note:
          infra.length === 0
            ? ''
            : $localize`:@@platform-operations.health.service.infrastructure.note:${infra.filter((entry) => entry.status === 'Healthy').length}:healthy: of ${infra.length}:count: checks pass`,
      },
    ];
    const planned = PLANNED.map((service): ServiceCard => ({
      ...service,
      state: service.number === NEXT_SERVICE ? 'next' : 'planned',
      note: '',
    }));
    return [...built, ...planned];
  });

  constructor() {
    const view = this.document.defaultView;
    if (view === null) return;
    const timer = view.setInterval(() => {
      if (this.live()) this.health.reload();
    }, LIVE_EVERY_MS);
    inject(DestroyRef).onDestroy(() => {
      view.clearInterval(timer);
    });
  }

  protected refresh(): void {
    this.health.reload();
    this.flags.reload();
  }

  protected toggleLive(): void {
    this.live.update((on) => !on);
  }

  protected tone(status: HealthStatus): 'ok' | 'warn' | 'stop' {
    return healthTone(status);
  }

  /** The chip's word for a check. */
  protected word(status: HealthStatus): string {
    return statusWords[status];
  }

  /** The chip's word for a service on the map. */
  protected stateWord(state: ServiceState): string {
    return stateWords[state];
  }

  protected stateTone(state: ServiceState): ServiceTone {
    return stateTones[state];
  }

  /** "12 ms", or nothing when the service wrote no duration. */
  protected took(entry: HealthEntry): string {
    const ms = durationMs(entry.duration);
    return ms === null ? '' : `${ms.toString()} ms`;
  }
}
