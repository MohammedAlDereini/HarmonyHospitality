import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SessionStore, SessionUser } from '@harmony/platform/session';
import { ACCESS_GRANTS, AccessGrants } from './access-grants';
import { IfAllowed } from './if-allowed.directive';
import { Permission } from './permission';

@Component({
  selector: 'hr-host',
  imports: [IfAllowed],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p *hrIfAllowed="permission">Users</p>`,
})
class Host {
  protected readonly permission: Permission = Permission.ViewUsers;
}

const ahmad: SessionUser = {
  sub: 'u1',
  name: 'Ahmad',
  tenant: 't1',
  roles: [],
  amr: ['pwd'],
  logoutUrl: '/bff/logout?sid=1',
};

describe('*hrIfAllowed', () => {
  const render = async (grants: AccessGrants | null): Promise<string> => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionStore, useValue: { user: signal(ahmad) } },
        { provide: ACCESS_GRANTS, useValue: () => grants },
      ],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await fixture.whenStable();
    return (fixture.nativeElement as HTMLElement).textContent.trim();
  };

  it('shows the content for a held permission', async () => {
    expect(
      await render({ permissions: new Set<Permission>([Permission.ViewUsers]), modules: {} }),
    ).toBe('Users');
  });

  it('shows nothing for a missing permission, and nothing while grants are unknown', async () => {
    expect(await render({ permissions: new Set<Permission>(), modules: {} })).toBe('');
    expect(await render(null)).toBe('');
  });
});
