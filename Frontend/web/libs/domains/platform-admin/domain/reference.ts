import { SortOrder } from '@harmony/shared/domain';

/** A reference row as Identity sends it: strings, numbers, flags, or nothing. */
export type ReferenceRow = Readonly<Record<string, string | number | boolean | null>>;

/** What a reference form holds: text for every field, a flag for a checkbox; numbers travel as text until sent. */
export type ReferenceValues = Readonly<Record<string, string | boolean>>;

/** One choice in a select. */
export interface ReferenceOption {
  readonly value: string;
  readonly label: string;
}

/**
 * Where a select's choices come from: another reference table, read when the form opens. With `narrowBy`, the
 * choices are read for the sibling field's value (the subdivisions of the chosen country).
 */
export interface OptionsSource {
  readonly table: string;
  readonly value: string;
  readonly label: string;
  readonly narrowBy?: string;
}

/** One field of a reference table's form. */
export interface ReferenceField {
  readonly key: string;
  readonly label: string;
  readonly kind: 'text' | 'number' | 'checkbox' | 'select';
  readonly required?: true;
  readonly maxLength?: number;
  /** The server's rule mirrored for speed; its code gives the wording. */
  readonly pattern?: RegExp;
  readonly patternCode?: string;
  /** The row's key: typed once, never changed. */
  readonly isKey?: true;
  readonly createOnly?: true;
  readonly editOnly?: true;
  readonly options?: readonly ReferenceOption[];
  readonly optionsFrom?: OptionsSource;
  readonly dir?: 'ltr' | 'rtl';
  readonly lang?: 'en' | 'ar';
  /** Under the input: the standard it follows and a real example. */
  readonly hint?: string;
  readonly placeholder?: string;
  /** May stay empty; the label says "(optional)". */
  readonly optional?: true;
  /** A number with decimals (latitude), otherwise whole. */
  readonly decimal?: true;
}

/** One column of a reference table's list. */
export interface ReferenceColumn {
  readonly key: string;
  readonly label: string;
  /** How the cell reads: text, a right-aligned number, Yes/No, Active/Inactive, a System mark, or Arabic text. */
  readonly kind?: 'text' | 'number' | 'bool' | 'onoff' | 'system' | 'rtl';
  /** The member Identity sorts by, when the column is sortable. */
  readonly sort?: string;
}

/** One filter above a reference table's list. */
export interface ReferenceFilter {
  readonly key: string;
  readonly label: string;
  readonly kind: 'text' | 'select' | 'lookup';
  readonly placeholder?: string;
  readonly options?: readonly ReferenceOption[];
  readonly optionsFrom?: OptionsSource;
}

/** One reference table the console edits through the generic screen. */
export interface ReferenceTable {
  /** The URL segment, e.g. "currencies". */
  readonly key: string;
  /** Identity's controller name, e.g. "Currency": /api/Currency/get-all, Create, Update, {id}. */
  readonly controller: string;
  readonly title: string;
  /** One row, in words: "currency". */
  readonly singular: string;
  /** What the table is for, under the title. */
  readonly description: string;
  /** The field that names a row in the URL and in Update/Delete. */
  readonly idField: string;
  readonly fields: readonly ReferenceField[];
  readonly columns: readonly ReferenceColumn[];
  readonly filters: readonly ReferenceFilter[];
  readonly defaultSort: readonly SortOrder[];
  /** Lookups may be deleted and are guarded by the lookup permissions. */
  readonly lookup?: true;
  readonly canDelete?: true;
  /** Lookup values belong to a category: the list is read for one category at a time. */
  readonly scopedBy?: string;
}
