import { ActivatedRouteSnapshot, Router } from '@angular/router';

/** The pattern of the current route (/p/:property/admin/account/:id): the configured paths, never the values. */
export const routePatternOf = (router: Router): string => {
  const parts: string[] = [];
  const walk = (snapshot: ActivatedRouteSnapshot): void => {
    const path = snapshot.routeConfig?.path;
    if (path !== undefined && path !== '') parts.push(path);
    if (snapshot.firstChild) walk(snapshot.firstChild);
  };
  walk(router.routerState.snapshot.root);
  return `/${parts.join('/')}`;
};
