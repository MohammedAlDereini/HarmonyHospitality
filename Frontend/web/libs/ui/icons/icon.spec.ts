import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Icon } from './icon';
import { icons } from './icon-set';

@Component({
  selector: 'hr-host',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hr-icon name="rooms" size="18" />
    <hr-icon name="go" [mirrored]="true" />
  `,
})
class Host {}

describe('hr-icon', () => {
  it('draws the prototype shapes in currentColor and mirrors direction icons', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await fixture.whenStable();
    const [rooms, go] = [...(fixture.nativeElement as HTMLElement).querySelectorAll('svg')];
    expect(rooms?.getAttribute('width')).toBe('18');
    expect(rooms?.getAttribute('viewBox')).toBe('0 0 16 16');
    expect(rooms?.querySelectorAll('path')).toHaveLength(icons.rooms.shapes.length - 1);
    expect(rooms?.querySelector('circle')?.getAttribute('r')).toBe('1.25');
    expect(rooms?.getAttribute('aria-hidden')).toBe('true');
    expect(go?.classList.contains('ic-dir')).toBe(true);
    expect(rooms?.classList.contains('ic-dir')).toBe(false);
  });

  it('a filled dot carries its own fill and no stroke', async () => {
    @Component({
      selector: 'hr-more-host',
      imports: [Icon],
      changeDetection: ChangeDetectionStrategy.OnPush,
      template: `<hr-icon name="more" />`,
    })
    class MoreHost {}
    const fixture = TestBed.createComponent(MoreHost);
    fixture.detectChanges();
    await fixture.whenStable();
    const first = (fixture.nativeElement as HTMLElement).querySelector('circle');
    expect(first?.getAttribute('fill')).toBe('currentColor');
    expect(first?.getAttribute('stroke')).toBe('none');
  });
});
