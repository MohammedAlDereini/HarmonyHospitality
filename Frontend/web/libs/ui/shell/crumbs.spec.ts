import { ActivatedRouteSnapshot } from '@angular/router';
import { crumbsOf } from './crumbs';

/** Only data, title and the child chain are read. */
const snapshot = (
  data: Record<string, unknown>,
  title: string | undefined,
  firstChild: ActivatedRouteSnapshot | null = null,
): ActivatedRouteSnapshot => ({ data, title, firstChild }) as unknown as ActivatedRouteSnapshot;

describe('crumbsOf', () => {
  it('collects the sections on the way and the deepest title as the page', () => {
    const root = snapshot(
      {},
      undefined,
      snapshot({ crumb: 'Administration' }, undefined, snapshot({}, 'Users')),
    );
    expect(crumbsOf(root)).toEqual({ trail: ['Administration'], page: 'Users' });
  });

  it('a route without titles has no page; a non-string crumb is ignored', () => {
    const NOT_A_CRUMB = 7;
    const root = snapshot({ crumb: NOT_A_CRUMB }, undefined, snapshot({}, undefined));
    expect(crumbsOf(root)).toEqual({ trail: [], page: null });
  });
});
