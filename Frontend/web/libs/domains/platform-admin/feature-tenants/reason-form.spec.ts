import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ReasonForm } from './reason-form';

@Component({
  selector: 'hr-host',
  imports: [ReasonForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hr-reason-form
    label="Reason"
    placeholder="Why"
    go="Suspend"
    [warning]="warning()"
    [problem]="problem()"
    (submitted)="reasons.set([...reasons(), $event])"
  />`,
})
class Host {
  readonly warning = signal<string | null>(null);
  readonly problem = signal<string | null>(null);
  readonly reasons = signal<readonly string[]>([]);
}

describe('hr-reason-form', () => {
  it('an empty reason stays in the browser; a reason is handed over trimmed; the warning and the problem show', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async (): Promise<void> => {
      await new Promise((resolve) => setTimeout(resolve, 1));
      fixture.detectChanges();
      await fixture.whenStable();
    };
    page.querySelector('form')?.requestSubmit();
    await settle();
    expect(fixture.componentInstance.reasons()).toEqual([]);
    expect(page.querySelector('#reason-box-error')?.textContent).toContain('Give a reason');
    const box = page.querySelector<HTMLInputElement>('#reason-box');
    if (box === null) throw new Error('no box');
    box.value = '  unpaid invoices ';
    box.dispatchEvent(new Event('input'));
    await settle();
    page.querySelector('form')?.requestSubmit();
    await settle();
    expect(fixture.componentInstance.reasons()).toEqual(['unpaid invoices']);
    expect(page.querySelector('.warning')).toBeNull();
    fixture.componentInstance.warning.set('This cannot be undone.');
    fixture.componentInstance.problem.set('That tenant is not active.');
    await settle();
    expect(page.querySelector('.warning')?.textContent).toContain('cannot be undone');
    expect(page.querySelector('button[type="submit"]')?.classList.contains('stop')).toBe(true);
    expect(page.textContent).toContain('not active');
  });
});
