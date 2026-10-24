import { ActivatedRouteSnapshot } from '@angular/router';

/** Where am I: the sections passed on the way (route data "crumb") and the page itself (the route title). */
export interface Crumbs {
  readonly trail: readonly string[];
  readonly page: string | null;
}

/** The crumb a route declares itself: `snapshot.data` is inherited by empty-path children and would repeat it. */
const crumbOf = (snapshot: ActivatedRouteSnapshot): string | null => {
  const crumb: unknown = snapshot.routeConfig?.data?.['crumb'];
  return typeof crumb === 'string' ? crumb : null;
};

const titleOf = (snapshot: ActivatedRouteSnapshot): string | null =>
  typeof snapshot.title === 'string' ? snapshot.title : null;

/** Walks the matched tree top down; the deepest title is the page, every crumb above it is the trail. */
export const crumbsOf = (root: ActivatedRouteSnapshot): Crumbs => {
  const chain = (snapshot: ActivatedRouteSnapshot): ActivatedRouteSnapshot[] => [
    snapshot,
    ...(snapshot.firstChild ? chain(snapshot.firstChild) : []),
  ];
  const snapshots = chain(root);
  const trail = snapshots.map(crumbOf).filter((crumb): crumb is string => crumb !== null);
  const page = snapshots
    .map(titleOf)
    .reduceRight<string | null>((found, title) => found ?? title, null);
  return { trail, page };
};
