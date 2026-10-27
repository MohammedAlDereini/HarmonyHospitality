import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MyPropertiesStatus, MyProperties } from './my-properties';
import { PropertyChooser } from './property-chooser';
import { PropertySummary } from './property-summary';

const grand: PropertySummary = {
  id: 'p-grd',
  code: 'GRD',
  nameEn: 'Grand Riyadh',
  nameAr: 'غراند الرياض',
  timeZoneId: 'Asia/Riyadh',
  currency: 'SAR',
};

/** The first read at creation plus the one Try again asks for. */
const READ_THEN_READ_AGAIN = 2;

describe('PropertyChooser', () => {
  const list = signal<readonly PropertySummary[]>([]);
  const status = signal<MyPropertiesStatus>('unknown');
  let reads = 0;

  const render = async (): Promise<HTMLElement> => {
    const fixture = TestBed.createComponent(PropertyChooser);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  };

  beforeEach(() => {
    list.set([]);
    status.set('unknown');
    reads = 0;
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: MyProperties,
          useValue: {
            list,
            status,
            ready: () => {
              reads += 1;
              return Promise.resolve();
            },
            reload: () => {
              reads += 1;
              return Promise.resolve();
            },
          },
        },
      ],
    });
  });

  it('asks for the hotels and says it is loading', async () => {
    const page = await render();
    expect(reads).toBe(1);
    expect(page.textContent).toContain('Loading your properties');
  });

  it('lists each hotel as a link to its property', async () => {
    status.set('ready');
    list.set([grand]);
    const page = await render();
    const link = page.querySelector('a');
    expect(link?.getAttribute('href')).toBe('/p/GRD');
    expect(link?.textContent).toContain('Grand Riyadh');
    expect(link?.textContent).toContain('GRD');
  });

  it('none assigned: says so', async () => {
    status.set('ready');
    const page = await render();
    expect(page.textContent).toContain('No property is assigned to you yet');
  });

  it('not loaded: offers Try again, which reads again', async () => {
    status.set('unavailable');
    const page = await render();
    page.querySelector('button')?.click();
    expect(reads).toBe(READ_THEN_READ_AGAIN);
  });
});
