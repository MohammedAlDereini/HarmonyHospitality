import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import {
  DirectoryStore,
  InvitationsStore,
  RolesStore,
  UsersStore,
} from '@harmony/administration/data-access';
import { DirectoryConnection, DirectoryGroup } from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { MyProperties } from '@harmony/platform/context';
import { ApiResult, IntentKey, newIntentKey } from '@harmony/platform/http';
import { IdentityConfirmation } from '@harmony/platform/session';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { DirectoryConsolePage } from './directory-console-page';

const OK = 200;
const NINE = 9;
const ALL_STAFF = 142;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const platformOps: DirectoryGroup = {
  id: 'dg1',
  displayName: 'Platform-Ops',
  externalId: '4a7c21e0-ops',
  roleId: 'r1',
  propertyId: null,
  propertyGroupId: null,
  members: NINE,
};
const allStaff: DirectoryGroup = {
  id: 'dg2',
  displayName: 'All-Staff',
  externalId: '0c3d99f1-all',
  roleId: null,
  propertyId: null,
  propertyGroupId: null,
  members: ALL_STAFF,
};
const connection: DirectoryConnection = {
  id: 'dc1',
  clientId: 'hrm-scim-3f9c21',
  isEnabled: true,
  secretIssuedAtUtc: '2026-10-29T09:30:00Z',
  scimUrl: 'https://identity.harmony.example/scim/v2',
  tokenUrl: 'https://identity.harmony.example/connect/token',
  people: ALL_STAFF,
  groups: [platformOps, allStaff],
};

class FakeStore {
  readonly connection = signal<DirectoryConnection | null>(connection);
  readonly groups = signal<readonly DirectoryGroup[]>([platformOps, allStaff]);
  readonly groupChoices = signal([]);
  readonly secret = signal(null);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  calls: string[] = [];
  reload(): void {
    this.calls.push('reload');
  }
  loadChoices(): void {
    this.calls.push('choices');
  }
  forgetSecret(): void {
    this.calls.push('forget');
  }
  switch(on: boolean): Promise<ApiResult<unknown>> {
    this.calls.push(`switch:${String(on)}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
  mapGroup(group: DirectoryGroup, roleId: string | null): Promise<ApiResult<unknown>> {
    this.calls.push(`map:${group.id}:${roleId ?? 'none'}`);
    return Promise.resolve({ kind: 'ok', status: OK, data: undefined });
  }
}

describe('DirectoryConsolePage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<DirectoryConsolePage>;
  let page: HTMLElement;

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) =>
      cell.textContent.replace(whitespace, ' ').trim(),
    );

  beforeEach(async () => {
    store = new FakeStore();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        {
          provide: UsersStore,
          useValue: {
            rows: signal([]),
            total: signal(0),
            census: signal(null),
            everyone: signal([]),
            countAll: () => undefined,
          },
        },
        { provide: InvitationsStore, useValue: { rows: signal([]), reload: () => undefined } },
        {
          provide: RolesStore,
          useValue: {
            roles: signal([
              { id: 'r1', code: 'PLATFORM-OPS', nameEn: 'Platform operations', nameAr: null },
            ]),
            loading: signal(false),
            problem: signal(null),
            load: () => undefined,
          },
        },
        { provide: MyProperties, useValue: { list: signal([]), ready: () => Promise.resolve() } },
        {
          provide: IdentityConfirmation,
          useValue: {
            run: (call: (intent: IntentKey) => Promise<ApiResult<unknown>>) => call(newIntentKey()),
          },
        },
        { provide: AccessStore, useValue: { can: () => true } },
      ],
    });
    TestBed.overrideComponent(DirectoryConsolePage, {
      set: { providers: [{ provide: DirectoryStore, useValue: store }] },
    });
    fixture = TestBed.createComponent(DirectoryConsolePage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  });

  it('says the connection and each group with the role it gives and where', () => {
    expect(texts('[data-connection] .c-card-h .chip')).toEqual(['On']);
    expect(texts('[data-connection] .c-facts dd')).toEqual([
      'https://identity.harmony.example/scim/v2',
      'https://identity.harmony.example/connect/token',
      'hrm-scim-3f9c21',
      'Oct 29, 2026 09:30 UTC',
      String(ALL_STAFF),
    ]);
    const rows = [...page.querySelectorAll('tr.grow')].map((row) =>
      [...row.querySelectorAll('td')]
        .slice(0, -1)
        .map((cell) => cell.textContent.replace(whitespace, ' ').trim()),
    );
    expect(rows).toEqual([
      ['Platform-Ops 4a7c…', String(NINE), 'Platform operations', 'Every hotel'],
      ['All-Staff 0c3d…', String(ALL_STAFF), 'Gives nothing', '—'],
    ]);
  });

  it('Change opens the mapping form in the drawer; Switch off goes to the store', async () => {
    page.querySelector<HTMLButtonElement>('tr.grow [data-act="map-group"]')?.click();
    await settle();
    expect(page.querySelector('dialog.drawer[open] #map-role')).not.toBeNull();
    expect(page.querySelector<HTMLSelectElement>('#map-role')?.value).toBe('r1');
    page.querySelector<HTMLButtonElement>('[data-act="switch-off"]')?.click();
    await settle();
    expect(store.calls).toContain('switch:false');
  });
});
