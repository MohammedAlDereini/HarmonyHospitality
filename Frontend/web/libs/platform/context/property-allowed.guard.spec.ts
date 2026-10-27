import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PartialMatchRouteSnapshot, UrlSegment } from '@angular/router';
import { SessionStore } from '@harmony/platform/session';
import { MyProperties } from './my-properties';
import { propertyAllowed } from './property-allowed.guard';
import { PropertySummary } from './property-summary';

const grand: PropertySummary = {
  id: 'p-grd',
  code: 'GRD',
  nameEn: 'Grand Riyadh',
  nameAr: 'غراند الرياض',
  timeZoneId: 'Asia/Riyadh',
  currency: 'SAR',
};

const noSnapshot = {} as PartialMatchRouteSnapshot;
const segments = (...parts: string[]): UrlSegment[] =>
  parts.map((part) => new UrlSegment(part, {}));

describe('propertyAllowed', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionStore, useValue: { ready: () => Promise.resolve('signed-in') } },
        {
          provide: MyProperties,
          useValue: { list: signal([grand]), ready: () => Promise.resolve() },
        },
      ],
    });
  });

  const run = (path: string, ...parts: string[]): Promise<unknown> =>
    Promise.resolve(
      TestBed.runInInjectionContext(() =>
        propertyAllowed({ path }, segments(...parts), noSnapshot),
      ),
    );

  it('one of my hotels matches; another code does not', async () => {
    expect(await run('p/:property', 'p', 'GRD')).toBe(true);
    expect(await run('p/:property', 'p', 'RYD')).toBe(false);
  });

  it('a route without the property parameter never matches', async () => {
    expect(await run('p', 'p')).toBe(false);
  });
});
