import { httpResource } from '@angular/common/http';
import { Injectable, computed } from '@angular/core';
import { PropertySummary, TenantSummary } from '@harmony/platform-operations/domain';
import { messageOf } from '@harmony/platform/errors';
import { Envelope, fromError } from '@harmony/platform/http';

export const tenantSummaryPath = '/api/Tenant/summary';
export const propertySummaryPath = '/api/Property/summary';

/**
 * The customers and the hotels in numbers, for the console's home (architecture 7.8): two reads, each with its own
 * failure, so one door down still leaves the other's numbers on the page. One read for the whole console: the home
 * and the bell share it.
 */
@Injectable({ providedIn: 'root' })
export class SummariesStore {
  private readonly tenantsRead = httpResource<Envelope<TenantSummary>>(() => ({
    url: tenantSummaryPath,
  }));
  private readonly propertiesRead = httpResource<Envelope<PropertySummary>>(() => ({
    url: propertySummaryPath,
  }));

  readonly tenants = computed<TenantSummary | null>(() =>
    this.tenantsRead.hasValue() ? (this.tenantsRead.value().data ?? null) : null,
  );
  readonly properties = computed<PropertySummary | null>(() =>
    this.propertiesRead.hasValue() ? (this.propertiesRead.value().data ?? null) : null,
  );
  readonly loading = computed(
    () => this.tenantsRead.isLoading() || this.propertiesRead.isLoading(),
  );
  readonly tenantsProblem = computed(() => {
    const error = this.tenantsRead.error();
    return error === undefined ? null : messageOf(fromError(error));
  });
  readonly propertiesProblem = computed(() => {
    const error = this.propertiesRead.error();
    return error === undefined ? null : messageOf(fromError(error));
  });

  reload(): void {
    this.tenantsRead.reload();
    this.propertiesRead.reload();
  }
}
