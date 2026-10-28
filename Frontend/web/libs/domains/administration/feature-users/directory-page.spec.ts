import { computed, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DirectoryStore, RolesStore } from '@harmony/administration/data-access';
import {
  AccessScope,
  DirectoryConnection,
  DirectoryGroup,
  DirectorySecret,
  GroupChoice,
  PrivilegeLevel,
  Role,
} from '@harmony/administration/domain';
import { AccessStore } from '@harmony/platform/access';
import { InstantFormatter, MyProperties, PropertySummary } from '@harmony/platform/context';
import { ApiResult, IntentKey } from '@harmony/platform/http';
import { DirectoryPage } from './directory-page';

const OK = 200;
const UUID_LENGTH = 36;
const THREE_PEOPLE = 3;
// Runs of whitespace collapse to one space: matches "a \n b" (→ "a b"); rejects "ab".
// Tester: https://regex101.com/?regex=%5Cs%2B&flags=g&testString=a%20%0A%20b
const whitespace = /\s+/g;

const frontDesk: Role = {
  id: 'r1',
  code: 'FRONT-DESK',
  nameEn: 'Front desk',
  nameAr: null,
  privilegeLevel: PrivilegeLevel.standard,
  isSystemRole: false,
  isSuperRole: false,
};
const amman: PropertySummary = {
  id: 'p1',
  code: 'AMM',
  nameEn: 'Ritz Amman',
  nameAr: 'ريتز عمّان',
  timeZoneId: 'Asia/Amman',
  currency: 'JOD',
};
const levant: GroupChoice = {
  id: 'pg1',
  code: 'LEVANT',
  typeCode: 'REGION',
  nameEn: 'Levant',
  nameAr: '',
};
const frontOffice: DirectoryGroup = {
  id: 'g1',
  displayName: 'Front office',
  externalId: 'entra-1',
  roleId: 'r1',
  propertyId: 'p1',
  propertyGroupId: null,
  members: THREE_PEOPLE,
};
const everyone: DirectoryGroup = {
  ...frontOffice,
  id: 'g2',
  displayName: 'Everyone',
  roleId: null,
  propertyId: null,
};
const connection: DirectoryConnection = {
  id: 'c1',
  clientId: 'a1b2',
  isEnabled: true,
  secretIssuedAtUtc: '2026-10-27T08:00:00Z',
  scimUrl: 'https://id.harmony.test/scim/v2',
  tokenUrl: 'https://id.harmony.test/scim/token',
  people: THREE_PEOPLE,
  groups: [frontOffice, everyone],
};
const shown: DirectorySecret = {
  clientId: 'a1b2',
  secret: 'not-a-real-secret',
  secretToken: 'a1b2.not-a-real-secret',
  scimUrl: connection.scimUrl,
  tokenUrl: connection.tokenUrl,
};

class FakeStore {
  readonly connection = signal<DirectoryConnection | null>(connection);
  readonly groups = computed(() => this.connection()?.groups ?? []);
  readonly loading = signal(false);
  readonly problem = signal<string | null>(null);
  readonly groupChoices = signal<readonly GroupChoice[]>([levant]);
  readonly secret = signal<DirectorySecret | null>(null);
  readonly calls: string[] = [];
  answer: ApiResult<unknown> = { kind: 'ok', status: OK, data: undefined };
  reload(): void {
    this.calls.push('reload');
  }
  loadChoices(): void {
    // The fake has its choices already.
  }
  forgetSecret(): void {
    this.secret.set(null);
  }
  open(k: IntentKey): Promise<ApiResult<DirectorySecret>> {
    this.calls.push(`open:${String(k.value.length)}`);
    this.secret.set(shown);
    return Promise.resolve({ kind: 'ok', status: OK, data: shown });
  }
  newSecret(): Promise<ApiResult<DirectorySecret>> {
    this.calls.push('new-secret');
    return Promise.resolve({ kind: 'ok', status: OK, data: shown });
  }
  switch(on: boolean): Promise<ApiResult<unknown>> {
    this.calls.push(`switch:${String(on)}`);
    return Promise.resolve(this.answer);
  }
  mapGroup(
    group: DirectoryGroup,
    roleId: string | null,
    scope: AccessScope | null,
  ): Promise<ApiResult<unknown>> {
    this.calls.push(
      `map:${group.id}:${roleId ?? '-'}:${scope?.propertyId ?? '-'}:${scope?.propertyGroupId ?? '-'}`,
    );
    return Promise.resolve(this.answer);
  }
}

describe('DirectoryPage', () => {
  let store: FakeStore;
  let fixture: ComponentFixture<DirectoryPage>;
  let page: HTMLElement;
  let mayManage = true;

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((node) =>
      node.textContent.replace(whitespace, ' ').trim(),
    );
  const click = (selector: string): void => {
    page.querySelector<HTMLElement>(selector)?.click();
  };
  const choose = async (selector: string, value: string): Promise<void> => {
    const box = page.querySelector<HTMLSelectElement>(selector);
    if (box === null) throw new Error(`no ${selector}`);
    box.value = value;
    box.dispatchEvent(new Event('input'));
    box.dispatchEvent(new Event('change'));
    await settle();
  };
  const render = async (): Promise<void> => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AccessStore, useValue: { can: () => mayManage } },
        { provide: InstantFormatter, useValue: { formatInstant: () => 'Oct 27, 2026, 08:00' } },
        {
          provide: MyProperties,
          useValue: { list: signal([amman]), ready: () => Promise.resolve() },
        },
      ],
    });
    TestBed.overrideComponent(DirectoryPage, {
      set: {
        providers: [
          { provide: DirectoryStore, useValue: store },
          {
            provide: RolesStore,
            useValue: {
              roles: signal([frontDesk]),
              loading: signal(false),
              problem: signal(null),
              load: () => undefined,
            },
          },
        ],
      },
    });
    fixture = TestBed.createComponent(DirectoryPage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  };

  beforeEach(async () => {
    store = new FakeStore();
    mayManage = true;
    await render();
  });

  it('a connection: on, its people, and each group with the role and the place it gives', () => {
    expect(texts('[data-connection] .sec-h .obj b')).toEqual(['On', String(THREE_PEOPLE)]);
    expect(texts('[data-group] .s-nm b')).toEqual(['Front office', 'Everyone']);
    expect(texts('[data-group]:first-child .obj b')).toEqual([
      String(THREE_PEOPLE),
      'Front desk',
      'Ritz Amman',
    ]);
    expect(texts('[data-group]:last-child .obj b').slice(1)).toEqual([
      'No role: gives nothing',
      'Every hotel',
    ]);
    expect(page.querySelector('[data-secret]')).toBeNull();
  });

  it('none connected: Open is one press; the secret shows once; I copied them hides it', async () => {
    store.connection.set(null);
    await settle();
    click('[data-act="open"]');
    await settle();
    expect(store.calls).toEqual([`open:${String(UUID_LENGTH)}`]);
    expect(texts('[data-secret-value]')).toEqual([shown.secret]);
    click('[data-act="copied"]');
    await settle();
    expect(page.querySelector('[data-secret]')).toBeNull();
  });

  it('a new secret asks first; Not now changes nothing', async () => {
    click('[data-act="new-secret"]');
    await settle();
    click('dialog[open] .dlg-a .btn:not(.solid)');
    await settle();
    expect(store.calls).toEqual([]);
    click('[data-act="new-secret"]');
    await settle();
    click('dialog[open] .dlg-a .solid');
    await settle();
    expect(store.calls).toEqual(['new-secret']);
  });

  it('Change opens the group editor; a place without a role is stopped here; a role at a hotel group is saved', async () => {
    click('[data-group]:last-child [data-act="map-group"]');
    await settle();
    expect(page.querySelector('[data-map-editor]')).not.toBeNull();
    await choose('#map-where', 'g:pg1');
    page.querySelector<HTMLFormElement>('[data-map-editor]')?.requestSubmit();
    await settle();
    expect(store.calls).toEqual([]);
    expect(texts('[data-map-editor] .field-error')).toEqual([
      'A hotel or a hotel group needs a role.',
    ]);
    await choose('#map-role', 'r1');
    page.querySelector<HTMLFormElement>('[data-map-editor]')?.requestSubmit();
    await settle();
    expect(store.calls).toEqual(['map:g2:r1:-:pg1']);
    expect(page.querySelector('[data-map-editor]')).toBeNull();
  });

  it('without the permissions: nothing to press', async () => {
    TestBed.resetTestingModule();
    mayManage = false;
    await render();
    expect(page.querySelector('[data-act]')).toBeNull();
  });
});
