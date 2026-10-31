import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { PropertiesStore, ReferenceStore, TenantsStore } from '@harmony/platform-admin/data-access';
import {
  Property,
  ReferenceRow,
  SubscriptionDraft,
  SubscriptionLine,
  Tenant,
  propertyTitle,
} from '@harmony/platform-admin/domain';
import { messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { Icon } from '@harmony/ui/icons';

/** What the form holds; numbers and dates travel as text until sent. */
interface SubscriptionValues {
  readonly moduleCode: string;
  readonly propertyId: string;
  readonly startsOn: string;
  readonly endsOn: string;
  readonly rooms: string;
  readonly terminals: string;
  readonly renewsAutomatically: boolean;
}

const empty: SubscriptionValues = {
  moduleCode: '',
  propertyId: '',
  startsOn: '',
  endsOn: '',
  rooms: '',
  terminals: '',
  renewsAutomatically: true,
};

/** A number typed as text, or null when the box was left empty. */
const numberOrNull = (text: string): number | null => (text.trim() === '' ? null : Number(text));
const dateFormat: Intl.DateTimeFormatOptions = {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
};
const textOf = (row: ReferenceRow, key: string): string => {
  const value = row[key];
  return typeof value === 'string' ? value : '';
};

/**
 * One more module for a tenant (platform console design, C3b): the module from the reference table, the scope as a
 * choice between the whole chain and one hotel, the rooms and terminals the line licenses, its dates, and whether it
 * renews; a sentence says what Add will do before it is pressed (SubscribeCommand, one intent key per press).
 */
@Component({
  selector: 'hr-subscription-editor',
  imports: [FormField, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './subscription-editor.html',
  styleUrl: './subscription-editor.css',
})
export class SubscriptionEditor {
  readonly tenant = input.required<Tenant>();
  /** A hotel to start on (a hotel's page asking for a line); null leaves the choice to the person. */
  readonly propertyId = input<string | null>(null);
  readonly done = output();
  readonly cancelled = output();

  private readonly store = inject(TenantsStore);
  private readonly reference = inject(ReferenceStore);
  protected readonly properties = inject(PropertiesStore);
  protected readonly modules = this.reference.choices('Module');
  private readonly moduleBox = viewChild.required<ElementRef<HTMLSelectElement>>('moduleBox');
  private readonly locale = $localize.locale ?? 'en';
  private readonly dates = new Intl.DateTimeFormat(
    this.locale === 'en' ? 'en-GB' : this.locale,
    dateFormat,
  );
  private readonly model = signal<SubscriptionValues>(empty);
  protected readonly form = form(this.model, (path) => {
    required(path.moduleCode, {
      message: $localize`:@@platform-admin.subscription-editor.module-required:Choose a module.`,
    });
    required(path.startsOn, {
      message: $localize`:@@platform-admin.subscriptions.starts-on.required:Give the start date.`,
    });
  });
  protected readonly problem = signal<string | null>(null);
  /** Chain-wide (the whole tenant) or one hotel: the two scope cards. */
  protected readonly oneHotel = computed(() => this.model().propertyId !== '');
  protected readonly chosenHotel = computed<Property | null>(() => {
    const id = this.model().propertyId;
    return id === '' ? null : (this.properties.rows().find((hotel) => hotel.id === id) ?? null);
  });
  protected readonly chosenModule = computed<ReferenceRow | null>(() => {
    const code = this.model().moduleCode;
    return code === ''
      ? null
      : (this.modules.rows().find((row) => this.moduleCode(row) === code) ?? null);
  });
  /** The enabled line this scope already holds for the chosen module: Add replaces it instead of adding a second. */
  protected readonly replacing = computed<SubscriptionLine | null>(() => {
    const value = this.model();
    if (value.moduleCode === '') return null;
    return (
      this.tenant().subscriptions.find(
        (line) =>
          line.isEnabled &&
          line.moduleCode === value.moduleCode &&
          (line.propertyId ?? '') === value.propertyId,
      ) ?? null
    );
  });

  /** "A new line HK · AMM-CTY · 60 rooms starts 15 Dec 2026 and renews. …": what Add will do. */
  protected readonly summary = computed(() => {
    const value = this.model();
    const module = this.chosenModule();
    if (module === null || value.startsOn === '') return '';
    const hotel = this.chosenHotel();
    const where =
      hotel === null
        ? $localize`:@@platform-admin.subscriptions.summary.chain:chain-wide`
        : hotel.code;
    const rooms =
      value.rooms.trim() === ''
        ? ''
        : $localize`:@@platform-admin.subscriptions.summary.rooms: · ${value.rooms.trim()}:ROOMS: rooms`;
    const ending = this.endingWord(value.renewsAutomatically, value.endsOn);
    const sees =
      hotel === null
        ? $localize`:@@platform-admin.subscriptions.summary.sees-all:every hotel sees ${this.moduleName(module)}:MODULE: at its next call.`
        : $localize`:@@platform-admin.subscriptions.summary.sees-one:the hotel sees ${this.moduleName(module)}:MODULE: at its next call.`;
    return $localize`:@@platform-admin.subscriptions.summary:A new line ${this.moduleCode(module)}:MODULE: · ${where}:WHERE:${rooms}:ROOMS: starts ${this.day(value.startsOn)}:STARTS: ${ending}:ENDING:. The tenant's subscriptions are published at once; ${sees}:SEES:`;
  });

  constructor() {
    this.modules.load();
    effect(() => {
      const propertyId = this.propertyId();
      untracked(() => {
        if (propertyId !== null) this.model.update((value) => ({ ...value, propertyId }));
      });
    });
    afterNextRender(() => {
      this.moduleBox().nativeElement.focus();
    });
  }

  /** "and renews", "and runs open-ended" or "and ends 31 Oct 2026". */
  private endingWord(renews: boolean, endsOn: string): string {
    if (renews) return $localize`:@@platform-admin.subscriptions.summary.renews:and renews`;
    if (endsOn === '')
      return $localize`:@@platform-admin.subscriptions.summary.open:and runs open-ended`;
    return $localize`:@@platform-admin.subscriptions.summary.ends:and ends ${this.day(endsOn)}:ENDS:`;
  }

  protected moduleCode(row: ReferenceRow): string {
    return textOf(row, 'code');
  }

  protected moduleName(row: ReferenceRow): string {
    const arabic = textOf(row, 'nameAr');
    return this.locale.startsWith('ar') && arabic !== '' ? arabic : textOf(row, 'nameEn');
  }

  /** "HK · Housekeeping · property level": the code, the name and what the module is sold per. */
  protected moduleLabel(row: ReferenceRow): string {
    const level =
      textOf(row, 'level') === 'Tenant'
        ? $localize`:@@platform-admin.subscriptions.level.tenant:tenant level`
        : $localize`:@@platform-admin.subscriptions.level.property:property level`;
    return `${this.moduleCode(row)} · ${this.moduleName(row)} · ${level}`;
  }

  protected propertyLabel(property: Property): string {
    return `${property.code} · ${propertyTitle(property, this.locale)}`;
  }

  /** "AMM-CTY · Levant City Suites · opens 15 Dec 2026", or the rooms when the hotel is open. */
  protected hotelNote(property: Property): string {
    const name = this.propertyLabel(property);
    return property.opensOn === null
      ? $localize`:@@platform-admin.subscriptions.hotel.rooms:${name}:HOTEL: · ${String(property.totalRooms)}:ROOMS: rooms`
      : $localize`:@@platform-admin.subscriptions.hotel.opens:${name}:HOTEL: · opens ${this.day(property.opensOn)}:OPENS:`;
  }

  protected chooseScope(oneHotel: boolean): void {
    this.model.update((value) => ({
      ...value,
      propertyId: oneHotel ? (this.properties.rows()[0]?.id ?? '') : '',
    }));
  }

  protected chooseHotel(id: string): void {
    this.model.update((value) => ({ ...value, propertyId: id }));
  }

  protected toggleRenews(on: boolean): void {
    this.model.update((value) => ({ ...value, renewsAutomatically: on }));
  }

  protected day(date: string): string {
    return this.dates.format(new Date(`${date}T00:00:00Z`));
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.problem.set(null);
      const value = field().value();
      const draft: SubscriptionDraft = {
        moduleCode: value.moduleCode,
        propertyId: value.propertyId === '' ? null : value.propertyId,
        startsOn: value.startsOn,
        endsOn: value.endsOn === '' ? null : value.endsOn,
        rooms: numberOrNull(value.rooms),
        terminals: numberOrNull(value.terminals),
        renewsAutomatically: value.renewsAutomatically,
      };
      const result = await this.store.subscribe(this.tenant(), draft, newIntentKey());
      this.settle(result);
      return undefined;
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  private settle(result: ApiResult<unknown>): void {
    if (result.kind === 'ok') {
      this.done.emit();
      return;
    }
    this.problem.set(messageOf(result));
  }
}
