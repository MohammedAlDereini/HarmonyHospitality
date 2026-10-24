import { Injectable, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { filter, map, startWith } from 'rxjs';

/** The route parameter: /p/:property/… */
export const propertyParameter = 'property';

/** The :property parameter wherever it sits in the matched tree, or null outside /p/… */
const propertyIn = (snapshot: ActivatedRouteSnapshot): string | null =>
  snapshot.paramMap.get(propertyParameter) ??
  (snapshot.firstChild ? propertyIn(snapshot.firstChild) : null);

/** Which property the URL is about right now, readable anywhere: the shell draws it, the context owns the rest. */
@Injectable({ providedIn: 'root' })
export class CurrentProperty {
  private readonly router = inject(Router);

  /** The code in the URL, or null outside a property. */
  readonly code = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => propertyIn(this.router.routerState.snapshot.root)),
      startWith(propertyIn(this.router.routerState.snapshot.root)),
    ),
    { requireSync: true },
  );
}
