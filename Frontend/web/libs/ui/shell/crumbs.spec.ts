import { ActivatedRouteSnapshot } from '@angular/router';
import { crumbsOf } from './crumbs';

/** Only the route's own data, the title and the child chain are read; `inherited` is what the router merged in. */
const snapshot = (
  data: Record<string, unknown>,
  title: string | undefined,
  firstChild: ActivatedRouteSnapshot | null = null,
  inherited: Record<string, unknown> = data,
): ActivatedRouteSnapshot =>
  ({
    data: inherited,
    title,
    firstChild,
    routeConfig: { data },
  }) as unknown as ActivatedRouteSnapshot;

describe('crumbsOf', () => {
  it('collects the sections on the way and the deepest title as the page', () => {
    const root = snapshot(
      {},
      undefined,
      snapshot({ crumb: 'Administration' }, undefined, snapshot({}, 'Users')),
    );
    expect(crumbsOf(root)).toEqual({ trail: ['Administration'], page: 'Users' });
  });

  it('a section inherited by an empty-path child is counted once', () => {
    const section = { crumb: 'Administration' };
    const root = snapshot(
      {},
      undefined,
      snapshot(section, undefined, snapshot({}, 'Users', null, section)),
    );
    expect(crumbsOf(root)).toEqual({ trail: ['Administration'], page: 'Users' });
  });

  it('a route without titles has no page; a non-string crumb is ignored', () => {
    const NOT_A_CRUMB = 7;
    const root = snapshot({ crumb: NOT_A_CRUMB }, undefined, snapshot({}, undefined));
    expect(crumbsOf(root)).toEqual({ trail: [], page: null });
  });
});
