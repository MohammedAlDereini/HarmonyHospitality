import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnInit,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  untracked,
} from '@angular/core';
import {
  FieldTree,
  FormField,
  disabled,
  form,
  maxLength,
  pattern,
  readonly,
  required,
  submit,
} from '@angular/forms/signals';
import { OptionRows, ReferenceStore } from '@harmony/platform-admin/data-access';
import {
  ReferenceField,
  ReferenceOption,
  ReferenceRow,
  ReferenceTable,
} from '@harmony/platform-admin/domain';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { fieldForCode } from './tables';

const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

/** What the form holds: every text, number and select as text; every checkbox as a flag. */
interface RowValues {
  readonly texts: Readonly<Record<string, string>>;
  readonly flags: Readonly<Record<string, boolean>>;
}

const asText = (
  value: string | number | boolean | readonly string[] | null | undefined,
): string => {
  if (value === null || value === undefined) return '';
  return Array.isArray(value) ? value.join(', ') : String(value);
};

/** A list field's text as Identity's list: the codes between the commas, trimmed, none empty; nothing typed is null. */
const asList = (text: string): string[] | null => {
  const codes = text
    .split(',')
    .map((code) => code.trim())
    .filter((code) => code !== '');
  return codes.length === 0 ? null : codes;
};

/**
 * One row of a reference table, new or changed, inside the drawer: the fields come from the table's description with
 * their hints, the choices from other tables (narrowed by a sibling where the table says so), the rules from Identity
 * mirrored for speed. A Signal Form over an editable copy, one intent key per press, Identity's codes back under
 * their fields (architecture 7.9, 14).
 */
@Component({
  selector: 'hr-row-editor',
  imports: [FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './row-editor.html',
  styleUrl: './row-editor.css',
})
export class RowEditor implements OnInit {
  readonly table = input.required<ReferenceTable>();
  /** The row being changed, or null for a new one. */
  readonly row = input<ReferenceRow | null>(null);
  readonly done = output();
  readonly cancelled = output();

  private readonly store = inject(ReferenceStore);
  private readonly injector = inject(Injector);
  protected readonly creating = computed(() => this.row() === null);
  /** The fields this form shows: never the create-only ones when editing, never the edit-only ones when creating. */
  protected readonly fields = computed(() =>
    this.table().fields.filter((field) =>
      this.creating() ? field.editOnly !== true : field.createOnly !== true,
    ),
  );
  private readonly model = linkedSignal<RowValues>(() => {
    const row = this.row();
    const texts: Record<string, string> = {};
    const flags: Record<string, boolean> = {};
    this.table().fields.forEach((field) => {
      if (field.kind === 'checkbox') flags[field.key] = row?.[field.key] === true;
      else texts[field.key] = asText(row?.[field.key]);
    });
    return { texts, flags };
  });
  /** Built once the table is known (ngOnInit): the schema's fields are the table's, so the input must be there. */
  protected form!: FieldTree<RowValues>;
  protected readonly problem = signal<string | null>(null);
  /** The other tables whose rows feed a select, by field. */
  private readonly sources = signal<ReadonlyMap<string, OptionRows>>(new Map());

  ngOnInit(): void {
    this.form = form(
      this.model,
      (path) => {
        this.table().fields.forEach((field) => {
          if (field.kind === 'checkbox') return;
          const at = path.texts[field.key];
          if (at === undefined) return;
          if (field.required === true)
            required(at, { message: wording(field.isKey === true ? '10401' : '10406') });
          if (field.maxLength !== undefined)
            maxLength(at, field.maxLength, { message: wording('10407') });
          if (field.pattern !== undefined)
            pattern(at, field.pattern, { message: wording(field.patternCode ?? '10402') });
          if (field.isKey === true) readonly(at, { when: () => !this.creating() });
          // A select narrowed by a sibling waits, disabled, until that sibling has a value.
          const narrowBy = field.optionsFrom?.narrowBy;
          if (narrowBy !== undefined) {
            disabled(at, { when: () => (this.model().texts[narrowBy] ?? '') === '' });
          }
        });
      },
      { injector: this.injector },
    );
    // A select narrowed by a sibling (the subdivisions of the chosen country) reads its choices for that value.
    effect(
      () => {
        this.table().fields.forEach((field) => {
          const from = field.optionsFrom;
          if (from?.narrowBy === undefined) return;
          const value = this.siblingValue(from.narrowBy);
          const source = this.sources().get(field.key);
          if (value !== '' && source !== undefined) source.load({ [from.narrowBy]: value });
        });
      },
      { injector: this.injector },
    );
  }

  constructor() {
    effect(() => {
      const table = this.table();
      untracked(() => {
        const next = new Map<string, OptionRows>();
        table.fields.forEach((field) => {
          const from = field.optionsFrom;
          if (from === undefined) return;
          const source = this.store.choices(from.table);
          if (from.narrowBy === undefined) source.load();
          next.set(field.key, source);
        });
        this.sources.set(next);
      });
    });
  }

  /** The field tree of one text field; every key of the table is in the model, so one is always there. */
  protected text(key: string): NonNullable<(typeof this.form.texts)[string]> {
    const tree = this.form.texts[key];
    if (tree === undefined) throw new Error(`no field ${key}`);
    return tree;
  }

  protected flag(key: string): NonNullable<(typeof this.form.flags)[string]> {
    const tree = this.form.flags[key];
    if (tree === undefined) throw new Error(`no flag ${key}`);
    return tree;
  }

  protected options(field: ReferenceField): readonly ReferenceOption[] {
    if (field.options !== undefined) return field.options;
    const from = field.optionsFrom;
    const source = this.sources().get(field.key);
    if (from === undefined || source === undefined) return [];
    if (from.narrowBy !== undefined && this.siblingValue(from.narrowBy) === '') return [];
    return source.rows().map((row) => ({
      value: asText(row[from.value]),
      label:
        from.value === 'id'
          ? asText(row[from.label])
          : `${asText(row[from.value])} · ${asText(row[from.label])}`,
    }));
  }

  /** The sibling a narrowed select waits for, while that sibling is still empty; null otherwise. */
  protected waitsFor(field: ReferenceField): ReferenceField | null {
    const narrowBy = field.optionsFrom?.narrowBy;
    if (narrowBy === undefined || this.siblingValue(narrowBy) !== '') return null;
    return this.table().fields.find((other) => other.key === narrowBy) ?? null;
  }

  protected chooseFirst(sibling: ReferenceField): string {
    return $localize`:@@platform-admin.reference.editor.choose-first:Choose ${sibling.label}:LABEL: first`;
  }

  protected errorId(field: ReferenceField): string {
    return `row-${field.key}-error`;
  }

  protected hintId(field: ReferenceField): string {
    return `row-${field.key}-hint`;
  }

  protected describedBy(field: ReferenceField): string | null {
    const ids: string[] = [];
    if (field.hint !== undefined) ids.push(this.hintId(field));
    if (this.text(field.key)().errors().length > 0) ids.push(this.errorId(field));
    return ids.length === 0 ? null : ids.join(' ');
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.problem.set(null);
      const body = this.body(field().value());
      const table = this.table();
      const result =
        this.row() === null
          ? await this.store.create(table.controller, body, newIntentKey())
          : await this.store.update(table.controller, body, newIntentKey());
      return this.settle(result, field);
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  private siblingValue(key: string): string {
    const tree = this.form.texts[key];
    return tree === undefined ? '' : tree().value();
  }

  /** Identity's command: texts trimmed, numbers as numbers, blanks as null, the flags, and the row's key when editing. */
  private body(values: RowValues): Record<string, unknown> {
    const table = this.table();
    const body: Record<string, unknown> = {};
    this.fields().forEach((field) => {
      if (field.kind === 'checkbox') {
        body[field.key] = values.flags[field.key] ?? false;
        return;
      }
      const text = (values.texts[field.key] ?? '').trim();
      if (field.kind === 'number') body[field.key] = text === '' ? null : Number(text);
      else if (field.list === true) body[field.key] = asList(text);
      else body[field.key] = text === '' && field.required !== true ? null : text;
    });
    const row = this.row();
    if (row !== null) body[table.idField] = row[table.idField];
    return body;
  }

  private settle(
    result: ApiResult<unknown>,
    field: typeof this.form,
  ):
    | { kind: string; message: string; fieldTree: NonNullable<(typeof field.texts)[string]> }[]
    | undefined {
    if (result.kind === 'ok') {
      this.done.emit();
      return undefined;
    }
    if ('codes' in result) {
      const table = this.table();
      const onFields = result.codes.flatMap((code) => {
        const name = fieldForCode(table, code);
        const tree = name === null ? undefined : field.texts[name];
        return tree === undefined
          ? []
          : [{ kind: `server-${code}`, message: wording(code), fieldTree: tree }];
      });
      if (onFields.length > 0) return onFields;
    }
    this.problem.set(messageOf(result));
    return undefined;
  }
}
