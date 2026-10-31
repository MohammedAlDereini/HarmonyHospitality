import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import { PopAt } from '@harmony/ui/shell';

/** One choice of a facet: the value the list filters by and the words shown. */
export interface FacetOption {
  readonly value: string;
  readonly label: string;
}

let facets = 0;

/**
 * A filter as a chip (platform console design, 11 Components): "Status: any" closed, "Status: Active, Suspended" when
 * narrowed, the choices in a native popover placed under it by PopAt (a popover lives in the top layer, so CSS insets
 * relative to the chip would land it at the viewport's bottom); Escape and a click outside close it.
 * Several choices when `multiple`, one otherwise; the empty value means "any". The list reads the change and narrows.
 */
@Component({
  selector: 'hr-facet',
  imports: [PopAt],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'facet' },
  templateUrl: './facet.html',
  styleUrl: './facet.css',
})
export class Facet {
  /** The facet's name: "Status", "Tier". */
  readonly label = input.required<string>();
  readonly options = input.required<readonly FacetOption[]>();
  /** The values chosen; empty = any. */
  readonly value = input<readonly string[]>([]);
  readonly multiple = input(false);
  readonly valueChange = output<readonly string[]>();

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly id = `facet-${String((facets += 1))}`;
  protected readonly anyLabel = $localize`:@@ui.data.facet.any:any`;
  protected readonly narrowed = computed(() => this.value().length > 0);
  /** What the chip says: the chosen labels, or "any". */
  protected readonly summary = computed(() => {
    const chosen = this.options().filter((option) => this.value().includes(option.value));
    return chosen.length === 0 ? this.anyLabel : chosen.map((option) => option.label).join(', ');
  });

  protected isChosen(option: FacetOption): boolean {
    return this.value().includes(option.value);
  }

  protected toggle(option: FacetOption, checked: boolean): void {
    if (!this.multiple()) {
      this.valueChange.emit(checked ? [option.value] : []);
      this.closePopover();
      return;
    }
    const without = this.value().filter((value) => value !== option.value);
    this.valueChange.emit(checked ? [...without, option.value] : without);
  }

  protected clear(): void {
    this.valueChange.emit([]);
    this.closePopover();
  }

  /** The popover closes itself on Escape and outside clicks; a choice made closes it too (not in jsdom). */
  private closePopover(): void {
    const popover = this.host.nativeElement.querySelector<HTMLElement>('[popover]');
    if (
      popover !== null &&
      typeof popover.hidePopover === 'function' &&
      popover.matches(':popover-open')
    ) {
      popover.hidePopover();
    }
  }
}
