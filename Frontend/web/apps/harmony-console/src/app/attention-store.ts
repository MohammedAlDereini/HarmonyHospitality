import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { ApprovalRequest } from '@harmony/administration/domain';
import { pendingApprovalsPath } from '@harmony/administration/data-access';
import {
  HealthStore,
  SummariesStore,
  trailCountsPath,
  trailPath,
} from '@harmony/platform-operations/data-access';
import {
  EndingLine,
  HealthEntry,
  PropertyLicence,
  TenantBrief,
  TrailCount,
  TrailEvent,
  countWhere,
  daysAgo,
  signInOutcomeOf,
  utcDate,
  utcMidnight,
} from '@harmony/platform-operations/domain';
import { AccessStore, Permission } from '@harmony/platform/access';
import { Envelope } from '@harmony/platform/http';
import { DESCENDING, PageRequest, Paged, SortOrder } from '@harmony/shared/domain';
import { AttentionItem } from '@harmony/ui/console-shell';

/** A failed administrator invitation older than this is no longer news. */
const INVITATION_WINDOW_DAYS = 7;
const RECENT_ROWS = 5;
const FAILED_ROWS = 10;
const FIRST_PAGE = 1;
const FAILED_INVITATION = 'tenant.administrator-invitation-failed';
const newestFirst: readonly SortOrder[] = [{ memberName: 'OccurredAtUtc', sortOrder: DESCENDING }];

interface TrailRequest extends PageRequest {
  readonly sortOrder: readonly SortOrder[];
  readonly action: string | null;
  readonly fromUtc: string | null;
}

const suspendedItem = (tenant: TenantBrief): AttentionItem => ({
  id: `suspended:${tenant.id}`,
  kind: $localize`:@@harmony-console.attention.suspended:Suspended`,
  text: $localize`:@@harmony-console.attention.suspended.text:${tenant.code}:code: · ${tenant.statusReason ?? ''}:reason:`,
  link: ['/tenants', tenant.id],
  tone: 'stop',
});

const healthItem = (entry: HealthEntry): AttentionItem => ({
  id: `health:${entry.name}`,
  kind: $localize`:@@harmony-console.attention.health:Health`,
  text: $localize`:@@harmony-console.attention.health.text:${entry.name}:check: · ${entry.status}:status:`,
  link: ['/health'],
  tone: entry.status === 'Degraded' ? 'warn' : 'stop',
});

const invitationItem = (row: TrailEvent): AttentionItem => ({
  id: `invitation:${row.id}`,
  kind: $localize`:@@harmony-console.attention.onboarding:Onboarding`,
  text: $localize`:@@harmony-console.attention.onboarding.text:${row.detail ?? ''}:code: · the administrator's invitation could not be sent`,
  link: ['/tenants'],
  tone: 'warn',
});

const licenceItem = (hotel: PropertyLicence): AttentionItem => ({
  id: `licence:${hotel.id}`,
  kind: $localize`:@@harmony-console.attention.licence:Licence`,
  text: $localize`:@@harmony-console.attention.licence.text:${hotel.code}:code: · ${hotel.totalRooms}:rooms: rooms declared, ${hotel.licensedRooms}:licensed: licensed`,
  link: ['/properties', hotel.id],
  tone: 'warn',
});

const approvalsItem = (count: number): AttentionItem => ({
  id: 'approvals',
  kind: $localize`:@@harmony-console.attention.approvals:Approvals`,
  text: $localize`:@@harmony-console.attention.approvals.text:${count}:count: waiting for a second administrator`,
  link: ['/people', 'security-rules'],
  tone: 'warn',
});

const endingItem = (line: EndingLine, locale: string): AttentionItem => ({
  id: `ending:${line.tenantId}:${line.propertyId ?? 'chain'}:${line.moduleCode}`,
  kind: $localize`:@@harmony-console.attention.expiring:Expiring`,
  text: $localize`:@@harmony-console.attention.expiring.text:${line.tenantCode}:code: · ${line.moduleCode}:module: ${line.propertyCode === null ? '' : `· ${line.propertyCode}`}:where: · ends ${utcDate(line.endsOn, locale)}:day:`,
  link: ['/tenants', line.tenantId],
  tone: 'info',
});

/**
 * What needs a platform person now, gathered from the doors the console already reads (architecture 7.8): suspended
 * customers, administrator invitations that could not be sent, hotels above their licence, fixed-term lines ending
 * soon, health checks that are not healthy, and approvals waiting in the platform's own tenant. Also the home's
 * recent trail and today's sign-in numbers. Every read is gated by the person's grants; a door that refuses is
 * simply silent here (the page it belongs to says why).
 */
@Injectable({ providedIn: 'root' })
export class AttentionStore {
  private readonly summaries = inject(SummariesStore);
  private readonly health = inject(HealthStore);
  private readonly access = inject(AccessStore);
  private readonly locale = $localize.locale ?? 'en';
  /** The moment the windows are measured from; moved by reload(), so the reads run again. */
  private readonly now = signal(new Date());

  private readonly approvals = httpResource<Envelope<ApprovalRequest[]>>(() =>
    this.access.can(Permission.ViewUsers) ? { url: pendingApprovalsPath } : undefined,
  );
  private readonly failed = httpResource<Envelope<Paged<TrailEvent>>>(() => {
    if (!this.access.can(Permission.ViewTenants)) return undefined;
    const body: TrailRequest = {
      pageNumber: FIRST_PAGE,
      pageSize: FAILED_ROWS,
      sortOrder: newestFirst,
      action: FAILED_INVITATION,
      fromUtc: daysAgo(INVITATION_WINDOW_DAYS, this.now()),
    };
    return { url: trailPath, method: 'POST', body };
  });
  private readonly recent = httpResource<Envelope<Paged<TrailEvent>>>(() => {
    if (!this.access.can(Permission.ViewTenants)) return undefined;
    const body: TrailRequest = {
      pageNumber: FIRST_PAGE,
      pageSize: RECENT_ROWS,
      sortOrder: newestFirst,
      action: null,
      fromUtc: null,
    };
    return { url: trailPath, method: 'POST', body };
  });
  private readonly todayCounts = httpResource<Envelope<TrailCount[]>>(() => {
    if (!this.access.can(Permission.ViewTenants)) return undefined;
    const query = new URLSearchParams({ fromUtc: utcMidnight(this.now()) });
    return { url: `${trailCountsPath}?${query.toString()}` };
  });

  /** The home's recent trail, newest first. */
  readonly recentTrail = computed<readonly TrailEvent[]>(() =>
    this.recent.hasValue() ? (this.recent.value().data?.results ?? []) : [],
  );
  private readonly counts = computed<readonly TrailCount[]>(() =>
    this.todayCounts.hasValue() ? (this.todayCounts.value().data ?? []) : [],
  );
  readonly signedInToday = computed(() =>
    countWhere(this.counts(), (a) => signInOutcomeOf(a) === 'signed-in'),
  );
  readonly refusedToday = computed(() =>
    countWhere(this.counts(), (a) => signInOutcomeOf(a) === 'refused'),
  );
  /** True once today's numbers came back (or were never asked for). */
  readonly countsKnown = computed(
    () => !this.access.can(Permission.ViewTenants) || this.todayCounts.hasValue(),
  );
  readonly pendingApprovals = computed<readonly ApprovalRequest[]>(() =>
    this.approvals.hasValue() ? (this.approvals.value().data ?? []) : [],
  );

  /** Everything that needs a person, most pressing first: critical, then attention, then the rest. */
  readonly items = computed<readonly AttentionItem[]>(() => {
    const tenants = this.summaries.tenants();
    const properties = this.summaries.properties();
    const report = this.health.report();
    const approvals = this.pendingApprovals();
    return [
      ...(tenants?.suspendedTenants ?? []).map(suspendedItem),
      ...(report?.entries ?? []).filter((entry) => entry.status !== 'Healthy').map(healthItem),
      ...(this.failed.hasValue() ? (this.failed.value().data?.results ?? []) : []).map(
        invitationItem,
      ),
      ...(properties?.aboveLicenceProperties ?? []).map(licenceItem),
      ...(approvals.length > 0 ? [approvalsItem(approvals.length)] : []),
      ...(tenants?.linesEndingSoon ?? []).map((line) => endingItem(line, this.locale)),
    ];
  });

  /** Reads everything again, with the windows measured from now. */
  reload(): void {
    this.now.set(new Date());
    this.recent.reload();
    this.summaries.reload();
    this.health.reload();
    this.approvals.reload();
  }
}
