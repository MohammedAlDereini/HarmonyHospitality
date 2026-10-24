import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AccessStore } from './access-store';
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

describe('*hrIfAllowed', () => {
  const held = signal<ReadonlySet<number>>(new Set());

  const render = async (): Promise<{ text: () => string; settle: () => Promise<void> }> => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AccessStore,
          useValue: { can: (permission: Permission) => held().has(permission) },
        },
      ],
    });
    const fixture = TestBed.createComponent(Host);
    const settle = async (): Promise<void> => {
      fixture.detectChanges();
      await fixture.whenStable();
    };
    await settle();
    return { text: () => (fixture.nativeElement as HTMLElement).textContent.trim(), settle };
  };

  it('shows the content only while the permission is held, and follows changes', async () => {
    held.set(new Set());
    const page = await render();
    expect(page.text()).toBe('');
    held.set(new Set([Permission.ViewUsers]));
    await page.settle();
    expect(page.text()).toBe('Users');
    held.set(new Set());
    await page.settle();
    expect(page.text()).toBe('');
  });
});
