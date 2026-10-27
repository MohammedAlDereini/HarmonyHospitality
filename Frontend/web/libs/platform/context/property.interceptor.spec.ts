import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideHarmonyHttp } from '@harmony/platform/http';
import { SessionStore } from '@harmony/platform/session';
import { MyProperties } from './my-properties';
import { PropertySummary } from './property-summary';
import { propertyHeader, propertyInterceptor } from './property.interceptor';

const grand: PropertySummary = {
  id: 'p-grd',
  code: 'GRD',
  nameEn: 'Grand Riyadh',
  nameAr: 'غراند الرياض',
  timeZoneId: 'Asia/Riyadh',
  currency: 'SAR',
};

describe('propertyInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'p/:property', children: [] }]),
        provideHarmonyHttp([propertyInterceptor]),
        provideHttpClientTesting(),
        { provide: SessionStore, useValue: { resumed: signal(0), user: signal(null) } },
        { provide: MyProperties, useValue: { list: signal([grand]) } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    backend.verify();
  });

  it('inside a hotel of mine every /api call names it by id; the BFF doors are left alone', async () => {
    await router.navigateByUrl('/p/GRD');
    http.get('/api/UserAccount/u1').subscribe();
    http.get('/bff/user').subscribe();
    expect(backend.expectOne('/api/UserAccount/u1').request.headers.get(propertyHeader)).toBe(
      'p-grd',
    );
    expect(backend.expectOne('/bff/user').request.headers.has(propertyHeader)).toBe(false);
  });

  it('outside a property, or at a code that is not among my hotels, no header is sent', async () => {
    http.get('/api/Role/get-all').subscribe();
    expect(backend.expectOne('/api/Role/get-all').request.headers.has(propertyHeader)).toBe(false);
    await router.navigateByUrl('/p/NYH');
    http.get('/api/Role/get-all').subscribe();
    expect(backend.expectOne('/api/Role/get-all').request.headers.has(propertyHeader)).toBe(false);
  });
});
