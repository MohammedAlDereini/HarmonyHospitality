import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { SessionStore } from '@harmony/platform/session';
import { filter, map, startWith } from 'rxjs';
import { MyProperties } from './my-properties';
import { PropertyScope } from './property-scope';
import { PropertySummary, propertyName } from './property-summary';

/** The route parameter: /p/:property/… */
export const propertyParameter = 'property';

/** The :property parameter wherever it sits in the matched tree, or null outside /p/… */
const propertyIn = (snapshot: ActivatedRouteSnapshot): string | null =>
  snapshot.paramMap.get(propertyParameter) ??
  (snapshot.firstChild ? propertyIn(snapshot.firstChild) : null);

/** The key every property-scoped cache uses (architecture 7.7, 9). */
export interface PropertyScopeKey {
  readonly tenant: string | null;
  readonly property: string | null;
}

const defaultInstantFormat: Intl.DateTimeFormatOptions = {
  dateStyle: 'medium',
  timeStyle: 'short',
};

/**
 * The property everything is about (architecture 4, 9): its code from the URL, its zone, currency and business date;
 * provided by providePropertyContext() on the /p/:property route.
 */
@Injectable()
export class PropertyContext {
  private readonly router = inject(Router);
  private readonly session = inject(SessionStore);
  private readonly properties = inject(MyProperties);
  private readonly resets = inject(PropertyScope);
  private readonly businessDateSignal = signal<string | null>(null);

  /** The code in the URL, or null outside a property. */
  readonly code = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => propertyIn(this.router.routerState.snapshot.root)),
      startWith(propertyIn(this.router.routerState.snapshot.root)),
    ),
    { requireSync: true },
  );

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
  formatInstant(instant: string | number | Date, options = defaultInstantFormat): string {
    const zone = this.zone();
    const formatter = new Intl.DateTimeFormat(
      $localize.locale ?? 'en',
      zone === null ? options : { ...options, timeZone: zone },
    );
    return formatter.format(typeof instant === 'string' ? new Date(instant) : instant);
  }
}
