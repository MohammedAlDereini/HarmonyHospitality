import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PageActions, PageActionsTemplate } from './page-actions';

/** Stands for the shell: renders whatever the current page offers. */
@Component({
  selector: 'hr-slot',
  imports: [NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="pacts">
    @if (actions.template(); as template) {
      <ng-container *ngTemplateOutlet="template" />
    }
  </div>`,
})
class Slot {
  protected readonly actions = inject(PageActions);
}

/** Stands for a page: offers a button while it is on screen. */
@Component({
  selector: 'hr-host',
  imports: [Slot, PageActionsTemplate],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hr-slot />
    @if (onScreen()) {
      <ng-template hrPageActions>
        <button type="button" class="pbtn" (click)="pressed.set(pressed() + 1)">Grid</button>
      </ng-template>
    }`,
})
class Host {
  readonly onScreen = signal(true);
  readonly pressed = signal(0);
}

describe('PageActions', () => {
  it("a page's template renders in the slot with the page's context; leaving the page empties the slot", async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const button = page.querySelector<HTMLButtonElement>('.pacts .pbtn');
    expect(button?.textContent).toBe('Grid');
    button?.click();
    expect(fixture.componentInstance.pressed()).toBe(1);
    fixture.componentInstance.onScreen.set(false);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(page.querySelector('.pacts .pbtn')).toBeNull();
    expect(TestBed.inject(PageActions).template()).toBeNull();
  });
});
