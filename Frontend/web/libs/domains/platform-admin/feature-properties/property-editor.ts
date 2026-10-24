import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, form, pattern, required, submit } from '@angular/forms/signals';
import { PropertiesStore, ReferenceStore, TenantsStore } from '@harmony/platform-admin/data-access';
import { PropertyDraft, ReferenceRow, Tenant, tenantName } from '@harmony/platform-admin/domain';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';

// A property code as Identity accepts it: 2 to 32 letters, digits, dots, dashes or underscores, with the surrounding
// spaces the form trims. Matches " AMM-2 "; rejects "A" (too short) and "AM M" (a space inside).
// Tester: https://regex101.com/?regex=%5E%5Cs*%5BA-Za-z0-9._-%5D%7B2%2C32%7D%5Cs*%24&testString=%20AMM-2%20
const propertyCode = /^\s*[A-Za-z0-9._-]{2,32}\s*$/;
// A whole number of rooms, 1 or more. Matches "120"; rejects "0" and "12a".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5B1-9%5D%5B0-9%5D*%5Cs*%24&testString=120
const roomsText = /^\s*[1-9][0-9]*\s*$/;
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

/** What the form holds; the number of rooms travels as text until sent. */
interface PropertyValues {
  readonly tenantId: string;
  readonly code: string;
  readonly nameEn: string;
  readonly nameAr: string;
  readonly propertyTypeCode: string;
  readonly cityId: string;
  readonly addressLine: string;
  readonly postalCode: string;
  readonly defaultCurrency: string;
  readonly defaultLanguageCode: string;
  readonly totalRooms: string;
  readonly legalEnvironmentCode: string;
  readonly taxEnvironmentCode: string;
  readonly accountingEnvironmentCode: string;
  readonly accommodationEnvironmentCode: string;
  readonly externalIdentifier: string;
  readonly startsOn: string;
  readonly moduleCode: string;
  readonly renewsAutomatically: boolean;
}

/** The text fields a server code can land on (never the flag). */
type TextField = Exclude<keyof PropertyValues, 'renewsAutomatically'>;

/** The field each of Identity's codes belongs to, whatever the kind of refusal. */
const fieldOfCode: Readonly<Record<string, TextField>> = {
  '10601': 'code',
  '10602': 'code',
  '00091': 'code',
  '10603': 'nameEn',
  '10604': 'nameEn',
  '10605': 'nameAr',
  '10606': 'nameAr',
  '10607': 'propertyTypeCode',
  '10608': 'cityId',
  '10609': 'defaultCurrency',
  '10610': 'defaultLanguageCode',
  '10611': 'totalRooms',
  '10613': 'addressLine',
  '10614': 'postalCode',
  '10615': 'externalIdentifier',
  '10616': 'moduleCode',
  '00088': 'moduleCode',
};

const empty: PropertyValues = {
  tenantId: '',
  code: '',
  nameEn: '',
  nameAr: '',
  propertyTypeCode: '',
  cityId: '',
  addressLine: '',
  postalCode: '',
  defaultCurrency: '',
  defaultLanguageCode: '',
  totalRooms: '',
  legalEnvironmentCode: '',
  taxEnvironmentCode: '',
  accountingEnvironmentCode: '',
  accommodationEnvironmentCode: '',
  externalIdentifier: '',
  startsOn: '',
  moduleCode: '',
  renewsAutomatically: true,
};

/**
 * A new property for a customer (CreatePropertyCommand): where it is, how it counts and pays, which regulatory
 * environments bind it, and the first module it starts with. Every choice comes from a reference table; one intent
 * key per press; Identity's codes back under their fields.
 */
@Component({
  selector: 'hr-property-editor',
  imports: [FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './property-editor.html',
  styleUrl: './property-editor.css',
})
export class PropertyEditor {
  /** The tenant the property is for, when the screen already knows it. */
  readonly tenantId = input<string | null>(null);
  readonly done = output();
  readonly cancelled = output();

  private readonly store = inject(PropertiesStore);
  private readonly reference = inject(ReferenceStore);
  protected readonly tenants = inject(TenantsStore);
  private readonly firstBox = viewChild.required<ElementRef<HTMLSelectElement>>('firstBox');
  private readonly locale = $localize.locale ?? 'en';
  protected readonly types = this.reference.choices('PropertyType');
  protected readonly cities = this.reference.choices('City');
  protected readonly currencies = this.reference.choices('Currency');
  protected readonly languages = this.reference.choices('Language');
  protected readonly environments = this.reference.choices('RegulatoryEnvironment');
  protected readonly modules = this.reference.choices('Module');
  protected readonly legal = computed(() => this.ofKind('Legal'));
  protected readonly tax = computed(() => this.ofKind('Tax'));
  protected readonly accounting = computed(() => this.ofKind('Accounting'));
  protected readonly accommodation = computed(() => this.ofKind('Accommodation'));

  private readonly model = signal<PropertyValues>(empty);
  protected readonly form = form(this.model, (path) => {
    required(path.tenantId, { message: wording('10501') });
    required(path.code, { message: wording('10601') });
    pattern(path.code, propertyCode, { message: wording('10602') });
    required(path.nameEn, { message: wording('10603') });
    required(path.nameAr, { message: wording('10605') });
    required(path.propertyTypeCode, { message: wording('10607') });
    required(path.cityId, { message: wording('10608') });
    required(path.defaultCurrency, { message: wording('10609') });
    required(path.defaultLanguageCode, { message: wording('10610') });
    required(path.totalRooms, { message: wording('10611') });
    pattern(path.totalRooms, roomsText, { message: wording('10611') });
    required(path.legalEnvironmentCode, { message: wording('10612') });
    required(path.taxEnvironmentCode, { message: wording('10612') });
    required(path.accountingEnvironmentCode, { message: wording('10612') });
    required(path.accommodationEnvironmentCode, { message: wording('10612') });
    required(path.startsOn, {
      message: $localize`:@@platform-admin.properties.starts-on.required:Give the start date.`,
    });
    required(path.moduleCode, { message: wording('10616') });
  });
  protected readonly problem = signal<string | null>(null);

  constructor() {
    this.types.load();
    this.cities.load();
    this.currencies.load();
    this.languages.load();
    this.environments.load();
    this.modules.load();
    afterNextRender(() => {
      const known = this.tenantId();
      if (known !== null) this.model.update((values) => ({ ...values, tenantId: known }));
      this.firstBox().nativeElement.focus();
    });
  }

  protected code(row: ReferenceRow): string {
    return String(row['code'] ?? row['id'] ?? '');
  }

  protected label(row: ReferenceRow): string {
    return String(row['nameEn'] ?? row['code'] ?? '');
  }

  protected tenantLabel(tenant: Tenant): string {
    return `${tenant.code} · ${tenantName(tenant, this.locale)}`;
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.problem.set(null);
      const value = field().value();
      const draft: PropertyDraft = {
        tenantId: value.tenantId,
        code: value.code.trim(),
        nameEn: value.nameEn.trim(),
        nameAr: value.nameAr.trim(),
        propertyTypeCode: value.propertyTypeCode,
        cityId: value.cityId,
        addressLine: value.addressLine.trim(),
        postalCode: value.postalCode.trim(),
        defaultCurrency: value.defaultCurrency,
        defaultLanguageCode: value.defaultLanguageCode,
        totalRooms: Number(value.totalRooms.trim()),
        legalEnvironmentCode: value.legalEnvironmentCode,
        taxEnvironmentCode: value.taxEnvironmentCode,
        accountingEnvironmentCode: value.accountingEnvironmentCode,
        accommodationEnvironmentCode: value.accommodationEnvironmentCode,
        externalIdentifier: value.externalIdentifier.trim(),
        startsOn: value.startsOn,
        lines: [
          {
            moduleCode: value.moduleCode,
            rooms: null,
            terminals: null,
            renewsAutomatically: value.renewsAutomatically,
          },
        ],
      };
      const result = await this.store.create(draft, newIntentKey());
      return this.settle(result, field);
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  private ofKind(kind: string): readonly ReferenceRow[] {
    return this.environments.rows().filter((row) => row['kind'] === kind);
  }

  private settle(
    result: ApiResult<unknown>,
    field: typeof this.form,
  ): { kind: string; message: string; fieldTree: typeof field.code }[] | undefined {
    if (result.kind === 'ok') {
      this.done.emit();
      return undefined;
    }
    if ('codes' in result) {
      const onFields = result.codes.flatMap((code) => {
        const name = fieldOfCode[code];
        return name === undefined
          ? []
          : [{ kind: `server-${code}`, message: wording(code), fieldTree: field[name] }];
      });
      if (onFields.length > 0) return onFields;
    }
    this.problem.set(messageOf(result));
    return undefined;
  }
}
