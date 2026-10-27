import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormField, form, maxLength, required, submit } from '@angular/forms/signals';
import { SettingsStore } from '@harmony/administration/data-access';
import {
  EffectiveSetting,
  ResolutionStep,
  SettingPlace,
  placesOf,
} from '@harmony/administration/domain';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { placeLabel, stepLabel, verdictLabel } from './wording';

/** Identity accepts a value of at most this many characters (ModelValidationErrorCodes 10811). */
const VALUE_MAX_LENGTH = 1000;
/** The value's own field codes: they land under the field, not in the general line. */
const valueCodes: readonly string[] = ['10810', '10811'];

const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

interface Draft {
  /** The index of the chosen place in the trace's places, as text (a select's value). */
  readonly place: string;
  readonly value: string;
}

/**
 * One key at the hotel, in the drawer: the trace (every place looked at, what it holds, how it fared) and, with
 * ManageSettings, a Signal Form that sets the value at one of the trace's places or clears it there. One intent key
 * per press; the value's field codes land under the field, every other outcome worded once. Identity decides who may
 * write where (a group or the whole customer needs the permission customer-wide, 00116).
 */
@Component({
  selector: 'hr-setting-detail',
  imports: [FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './setting-detail.html',
  styleUrl: './setting-detail.css',
})
export class SettingDetail {
  readonly setting = input.required<EffectiveSetting>();
  /** With ManageSettings the form is offered; the API refuses on its own anyway. */
  readonly mayManage = input(false);
  /** The value was set; the drawer may close. */
  readonly saved = output();
  /** The value was cleared; the drawer may close. */
  readonly cleared = output();
  readonly cancelled = output();

  private readonly store = inject(SettingsStore);
  protected readonly places = computed<readonly SettingPlace[]>(() => placesOf(this.setting()));
  private readonly model = signal<Draft>({ place: '0', value: '' });
  protected readonly chosen = computed<SettingPlace | null>(
    () => this.places()[Number(this.model().place)] ?? null,
  );
  /** A bool reads true or false from a select; a time from the time input; the rest as typed. */
  protected readonly kind = computed(() => this.setting().dataType);

  protected readonly form = form(this.model, (path) => {
    required(path.value, { message: wording('10810') });
    maxLength(path.value, VALUE_MAX_LENGTH, { message: wording('10811') });
  });

  protected readonly problem = signal<string | null>(null);
  protected readonly clearing = signal(false);

  protected stepText(step: ResolutionStep): string {
    return stepLabel(step);
  }

  protected verdictText(step: ResolutionStep): string {
    return verdictLabel(step.verdict);
  }

  protected placeText(place: SettingPlace): string {
    return placeLabel(place);
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.problem.set(null);
      const place = this.chosen();
      if (place === null) return undefined;
      // The key is this press's own: a retry inside the call reuses it, a new press is a new intent.
      const result = await this.store.set(
        this.setting().key,
        place,
        field().value().value.trim(),
        newIntentKey(),
      );
      if (result.kind === 'ok') {
        this.saved.emit();
        return undefined;
      }
      return this.onField(result, field);
    });
  }

  protected clear(): void {
    void this.clearNow();
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  private async clearNow(): Promise<void> {
    const place = this.chosen();
    if (place === null) return;
    this.problem.set(null);
    this.clearing.set(true);
    const result = await this.store.clear(this.setting().key, place, newIntentKey());
    this.clearing.set(false);
    if (result.kind === 'ok') {
      this.cleared.emit();
      return;
    }
    this.problem.set(messageOf(result));
  }

  /** The value's codes go under the field; everything else becomes the general line. */
  private onField(
    result: Exclude<ApiResult<unknown>, { kind: 'ok' }>,
    field: typeof this.form,
  ): { kind: string; message: string; fieldTree: typeof field.value }[] | undefined {
    if (result.kind === 'invalid') {
      const onValue = result.codes.filter((code) => valueCodes.includes(code));
      if (onValue.length > 0) {
        return onValue.map((code) => ({
          kind: `server-${code}`,
          message: wording(code),
          fieldTree: field.value,
        }));
      }
    }
    this.problem.set(messageOf(result));
    return undefined;
  }
}
