import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Facet, FacetOption } from './facet';

const options: readonly FacetOption[] = [
  { value: 'Active', label: 'Active' },
  { value: 'Suspended', label: 'Suspended' },
  { value: 'Terminated', label: 'Terminated' },
];

@Component({
  imports: [Facet],
  template: `
    <hr-facet
      label="Status"
      [options]="options"
      [value]="value()"
      [multiple]="multiple"
      (valueChange)="value.set($event)"
    />
  `,
})
class Host {
  readonly options = options;
  readonly value = signal<readonly string[]>([]);
  multiple = true;
}

/** The nth choice in a list of inputs; the test fails plainly when it is not there. */
const pick = (list: NodeListOf<HTMLInputElement>, index: number): HTMLInputElement => {
  const box = list[index];
  if (box === undefined) throw new Error(`no choice ${String(index)}`);
  return box;
};

describe('Facet', () => {
  let fixture: ComponentFixture<Host>;
  let page: HTMLElement;

  // Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
  const whitespace = /\s+/g;
  const text = (selector: string): string =>
    page.querySelector(selector)?.textContent.replace(whitespace, ' ').trim() ?? '';

  beforeEach(async () => {
    TestBed.configureTestingModule({ imports: [Host] });
    fixture = TestBed.createComponent(Host);
    page = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('says "any" until a choice is made, then names the choices and marks the chip', async () => {
    expect(text('.chip-btn')).toBe('Status: any');
    expect(page.querySelector('.chip-btn.on')).toBeNull();
    const boxes = page.querySelectorAll<HTMLInputElement>('.opts input');
    expect([...boxes].map((box) => box.type)).toEqual(['checkbox', 'checkbox', 'checkbox']);
    pick(boxes, 0).checked = true;
    pick(boxes, 0).dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();
    pick(boxes, 1).checked = true;
    pick(boxes, 1).dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.componentInstance.value()).toEqual(['Active', 'Suspended']);
    expect(text('.chip-btn')).toBe('Status: Active, Suspended');
    expect(page.querySelector('.chip-btn.on')).not.toBeNull();
    page.querySelector<HTMLButtonElement>('.clear')?.click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.componentInstance.value()).toEqual([]);
  });

  it('one choice at a time when not multiple: radios, and a new choice replaces the old', async () => {
    fixture.componentInstance.multiple = false;
    fixture.componentInstance.value.set(['Active']);
    fixture.detectChanges();
    await fixture.whenStable();
    const radios = page.querySelectorAll<HTMLInputElement>('.opts input');
    expect([...radios].map((box) => box.type)).toEqual(['radio', 'radio', 'radio']);
    expect(pick(radios, 0).checked).toBe(true);
    pick(radios, 2).checked = true;
    pick(radios, 2).dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.componentInstance.value()).toEqual(['Terminated']);
    expect(text('.chip-btn')).toBe('Status: Terminated');
  });
});
