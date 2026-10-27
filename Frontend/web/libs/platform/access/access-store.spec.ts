import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CurrentProperty } from '@harmony/platform/context';
import { API_RETRY_DELAYS, HttpStatusEvents, provideHarmonyHttp } from '@harmony/platform/http';
import { SessionStore } from '@harmony/platform/session';
import { myPermissionsPath } from './access-grants';
import { AccessStore } from './access-store';
import { Permission } from './permission';

const SERVICE_UNAVAILABLE = 503;
const ATTEMPTS = 3;
const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

describe('AccessStore', () => {
  let access: AccessStore;
  let http: HttpTestingController;
  let events: HttpStatusEvents;
  const resumed = signal(0);
  const hotel = signal<string | null>(null);

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await tick();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHarmonyHttp(),
        provideHttpClientTesting(),
        { provide: API_RETRY_DELAYS, useValue: [0, 0] },
        { provide: SessionStore, useValue: { resumed, user: signal(null) } },
        { provide: CurrentProperty, useValue: { code: hotel } },
      ],
    });
    hotel.set(null);
    access = TestBed.inject(AccessStore);
    http = TestBed.inject(HttpTestingController);
    events = TestBed.inject(HttpStatusEvents);
  });

  afterEach(() => {
    http.verify();
  });

  it('nothing is known, nothing is allowed, until the door answers', async () => {
    expect(access.known()).toBe(false);
    expect(access.can(Permission.ViewUsers)).toBe(false);
    const ready = access.ready();
    void access.ready();
    const request = http.expectOne(myPermissionsPath);
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('x-csrf')).toBe('1');
    request.flush({ data: [Permission.ViewUsers, Permission.ViewProperties] });
    await ready;
    expect(access.known()).toBe(true);
    expect(access.can(Permission.ViewUsers)).toBe(true);
    expect(access.can(Permission.ManageUsers)).toBe(false);
  });

  it('the door not answering leaves everything hidden; reload asks again', async () => {
    const ready = access.ready();
    await Array.from({ length: ATTEMPTS }).reduce<Promise<void>>(async (previous) => {
      await previous;
      http
        .expectOne(myPermissionsPath)
        .flush(null, { status: SERVICE_UNAVAILABLE, statusText: 'Down' });
      await tick();
    }, Promise.resolve());
    await ready;
    expect(access.status()).toBe('unavailable');
    expect(access.can(Permission.ViewUsers)).toBe(false);
    const again = access.reload();
    http.expectOne(myPermissionsPath).flush({ data: [Permission.ViewUsers] });
    await again;
    expect(access.can(Permission.ViewUsers)).toBe(true);
  });

  it('modules are not in the payload yet: no line is held', async () => {
    const ready = access.ready();
    http.expectOne(myPermissionsPath).flush({ data: [] });
    await ready;
    expect(access.entitled('front-office', 'GRD')).toBe(false);
  });

  it('another hotel in the URL reads the grants again; the same hotel does not', async () => {
    const ready = access.ready();
    http.expectOne(myPermissionsPath).flush({ data: [] });
    await ready;
    hotel.set('GRD');
    await settle();
    http.expectOne(myPermissionsPath).flush({ data: [Permission.ViewUsers] });
    await tick();
    expect(access.can(Permission.ViewUsers)).toBe(true);
    hotel.set('GRD');
    await settle();
    http.expectNone(myPermissionsPath);
    hotel.set('NYH');
    await settle();
    http.expectOne(myPermissionsPath).flush({ data: [] });
    await tick();
    expect(access.can(Permission.ViewUsers)).toBe(false);
  });

  it('a 403 reads the grants once more; a second 403 soon after does not', async () => {
    const ready = access.ready();
    http.expectOne(myPermissionsPath).flush({ data: [] });
    await ready;
    events.forbidden.update((n) => n + 1);
    await settle();
    http.expectOne(myPermissionsPath).flush({ data: [Permission.ViewUsers] });
    await tick();
    expect(access.can(Permission.ViewUsers)).toBe(true);
    events.forbidden.update((n) => n + 1);
    await settle();
    http.expectNone(myPermissionsPath);
  });

  it('after a separate-window sign-in finished, the grants are read again', async () => {
    const ready = access.ready();
    http.expectOne(myPermissionsPath).flush({ data: [] });
    await ready;
    resumed.update((n) => n + 1);
    await settle();
    http.expectOne(myPermissionsPath).flush({ data: [Permission.ViewRoles] });
    await tick();
    expect(access.can(Permission.ViewRoles)).toBe(true);
  });
});
