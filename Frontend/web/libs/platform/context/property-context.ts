import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { SessionStore } from '@harmony/platform/session';
import { CurrentProperty } from './current-property';
import { InstantFormatter, formatInZone } from './instant-formatter';
import { MyProperties } from './my-properties';
import { PropertyScope } from './property-scope';
import { PropertySummary, propertyName } from './property-summary';

/** The key every property-scoped cache uses (architecture 7.7, 9). */
export interface PropertyScopeKey {
  readonly tenant: string | null;
  readonly property: string | null;
}

/**
 * The property everything is about (architecture 4, 9): its code from the URL, its zone, currency and business date;
 * provided by providePropertyContext() on the /p/:property route.
 */
@Injectable()
export class PropertyContext extends InstantFormatter {
  private readonly session = inject(SessionStore);
  private readonly properties = inject(MyProperties);
  private readonly resets = inject(PropertyScope);
  private readonly businessDateSignal = signal<string | null>(null);

  /** The code in the URL, or null outside a property. */
  readonly code = inject(CurrentProperty).code;

  readonly property = computed<PropertySummary | null>(() => {
    const code = this.code();
    return code === null ? null : (this.properties.list().find((p) => p.code === code) ?? null);
  });

  readonly zone = computed(() => this.property()?.timeZoneId ?? null);
  readonly currency = computed(() => this.property()?.currency ?? null);
  readonly name = computed(() => {
    const property = this.property();
    return property === null ? null : propertyName(property, $localize.locale ?? 'en');
  });

  /** YYYY-MM-DD as the server says it (the night audit moves it), never the device clock. Null until a service answers. */
  readonly businessDate = this.businessDateSignal.asReadonly();

  readonly scope = computed<PropertyScopeKey>(() => ({
    tenant: this.session.user()?.tenant ?? null,
    property: this.code(),
  }));

  constructor() {
    super();
    let previous = untracked(this.code);
    effect(() => {
      const code = this.code();
      untracked(() => {
        if (code !== previous) {
          previous = code;
          this.resets.reset();
        }
      });
    });
  }

  businessDateFromServer(date: string): void {
    this.businessDateSignal.set(date);
  }

  /** An instant shown in the property's zone, in the build's language; the device's zone plays no part. */
  formatInstant(instant: string | number | Date, options?: Intl.DateTimeFormatOptions): string {
    return formatInZone(instant, this.zone(), options);
  }
}
