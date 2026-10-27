import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, RouterOutlet, provideRouter } from '@angular/router';
import { SessionStore, SessionUser } from '@harmony/platform/session';
import { MyProperties } from './my-properties';
import { PropertyContext } from './property-context';
import { PropertyScope } from './property-scope';
import { PropertySummary } from './property-summary';
import { providePropertyContext } from './provide';

const ahmad: SessionUser = {
  sub: 'u1',
  name: 'Ahmad',
  tenant: 't1',
  roles: [],
  amr: ['pwd'],
  logoutUrl: '/bff/logout?sid=1',
};

const grand: PropertySummary = {
  id: 'p-grd',
  code: 'GRD',
  nameEn: 'Grand Riyadh',
  nameAr: 'غراند الرياض',
  timeZoneId: 'Asia/Riyadh',
  currency: 'SAR',
};
const harbour: PropertySummary = {
  id: 'p-nyh',
  code: 'NYH',
  nameEn: 'Harbour New York',
  nameAr: 'هاربر نيويورك',
  timeZoneId: 'America/New_York',
  currency: 'USD',
};

@Component({
  selector: 'hr-inside',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '',
})
class Inside {
  readonly context = inject(PropertyContext);
}

@Component({
  selector: 'hr-root-host',
  imports: [RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '<router-outlet />',
})
class RootHost {}

/** One instant, shown in two zones: 22:30 UTC on 24 October is 01:30 next day in Riyadh, 18:30 in New York. */
const instant = '2026-10-24T22:30:00Z';

describe('PropertyContext', () => {
  let router: Router;
  let resets: number;

  const inside = async (url: string): Promise<PropertyContext> => {
    const fixture = TestBed.createComponent(RootHost);
    await router.navigateByUrl(url);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.debugElement.query(By.directive(Inside)).componentInstance as Inside;
    return page.context;
  };

  beforeEach(() => {
    resets = 0;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          {
            path: 'p/:property',
            providers: [providePropertyContext()],
            children: [
              { path: '', component: Inside },
              { path: 'arrivals', component: Inside },
            ],
          },
        ]),
        { provide: SessionStore, useValue: { user: signal(ahmad), resumed: signal(0) } },
        {
          provide: MyProperties,
          useValue: {
            list: signal([grand, harbour]),
            status: signal('ready'),
            ready: () => Promise.resolve(),
          },
        },
      ],
    });
    router = TestBed.inject(Router);
    TestBed.inject(PropertyScope).onReset(() => {
      resets += 1;
    });
  });

  it('knows the property from the URL: code, name, zone, currency, and the cache scope', async () => {
    const context = await inside('/p/GRD');
    expect(context.code()).toBe('GRD');
    expect(context.name()).toBe('Grand Riyadh');
    expect(context.zone()).toBe('Asia/Riyadh');
    expect(context.currency()).toBe('SAR');
    expect(context.scope()).toEqual({ tenant: 't1', property: 'GRD' });
    expect(context.businessDate()).toBeNull();
  });

  it('shows an instant in the property zone, whatever the device zone is', async () => {
    const riyadh = await inside('/p/GRD');
    expect(riyadh.formatInstant(instant)).toBe('Oct 25, 2026, 1:30 AM');
    await router.navigateByUrl('/p/NYH');
    expect(riyadh.formatInstant(instant)).toBe('Oct 24, 2026, 6:30 PM');
  });

  it('a property switch resets the scope once; moving inside the same property does not', async () => {
    const context = await inside('/p/GRD');
    await router.navigateByUrl('/p/GRD/arrivals');
    TestBed.tick();
    expect(resets).toBe(0);
    await router.navigateByUrl('/p/NYH');
    TestBed.tick();
    expect(context.code()).toBe('NYH');
    expect(resets).toBe(1);
  });

  it('the business date comes from the server, never the device clock', async () => {
    const context = await inside('/p/GRD');
    context.businessDateFromServer('2026-10-24');
    expect(context.businessDate()).toBe('2026-10-24');
  });
});
