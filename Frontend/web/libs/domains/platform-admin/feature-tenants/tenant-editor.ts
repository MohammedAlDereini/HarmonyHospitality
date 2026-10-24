import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  viewChild,
} from '@angular/core';
import {
  FormField,
  email,
  form,
  maxLength,
  pattern,
  readonly,
  required,
  submit,
} from '@angular/forms/signals';
import { ReferenceStore, TenantsStore } from '@harmony/platform-admin/data-access';
import {
  ReferenceRow,
  TENANT_MAX_TIER,
  TENANT_MIN_TIER,
  Tenant,
  TenantDraft,
} from '@harmony/platform-admin/domain';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';

const NAME_MAX_LENGTH = 128;
const EMAIL_MAX_LENGTH = 256;
// A tenant code as Identity accepts it: 2 to 32 letters, digits, dots, dashes or underscores, with the surrounding
// spaces the form trims. Matches " blue-nile "; rejects "b" (too short) and "blue nile" (a space inside).
// Tester: https://regex101.com/?regex=%5E%5Cs*%5BA-Za-z0-9._-%5D%7B2%2C32%7D%5Cs*%24&testString=%20blue-nile%20
const tenantCode = /^\s*[A-Za-z0-9._-]{2,32}\s*$/;
/** The field each of Identity's codes belongs to, whatever the kind of refusal. */
const fieldOfCode: Readonly<Record<string, keyof TenantDraft>> = {
  '10502': 'code',
  '10503': 'code',
  '00071': 'code',
  '10504': 'legalNameEn',
  '10505': 'legalNameEn',
  '10506': 'legalNameAr',
  '10507': 'legalNameAr',
  '10508': 'countryCode',
  '10509': 'countryCode',
  '10510': 'defaultCurrency',
  '10511': 'defaultCurrency',
  '10512': 'externalIdentifier',
  '10515': 'tier',
  '10516': 'administratorEmail',
  '10517': 'administratorEmail',
  '10518': 'administratorEmail',
  '00085': 'administratorEmail',
  '10519': 'administratorDisplayName',
  '10520': 'administratorDisplayName',
};
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');
/** The tiers as a select carries them: text; the draft gets the number. */
const tiers: readonly string[] = Array.from(
  { length: TENANT_MAX_TIER - TENANT_MIN_TIER + 1 },
  (_, index) => String(TENANT_MIN_TIER + index),
);

/** What the form holds: the draft with the tier as text, the way a select carries it. */
interface TenantValues extends Omit<TenantDraft, 'tier'> {
  readonly tier: string;
}

const empty: TenantValues = {
  code: '',
  legalNameEn: '',
  legalNameAr: '',
  countryCode: '',
  defaultCurrency: '',
  tier: String(TENANT_MIN_TIER),
  externalIdentifier: '',
  administratorEmail: '',
  administratorDisplayName: '',
};

/**
 * A customer's code, legal names, country, currency, tier and, when new, its first administrator (invited by mail).
 * A Signal Form over an editable copy (architecture 7.9), the countries and currencies from the reference tables,
 * one intent key per press, Identity's codes back under their fields.
 */
@Component({
  selector: 'hr-tenant-editor',
  imports: [FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './tenant-editor.html',
  styleUrl: './tenant-editor.css',
})
export class TenantEditor {
  /** The tenant being edited, or null for a new one. */
  readonly tenant = input<Tenant | null>(null);
  readonly done = output();
  readonly cancelled = output();

  private readonly store = inject(TenantsStore);
  private readonly reference = inject(ReferenceStore);
  private readonly firstBox = viewChild.required<ElementRef<HTMLInputElement>>('firstBox');
  protected readonly countries = this.reference.choices('Country');
  protected readonly currencies = this.reference.choices('Currency');
  protected readonly tiers = tiers;
  private readonly model = linkedSignal<TenantValues>(() => {
    const tenant = this.tenant();
    return tenant === null
      ? empty
      : {
          ...empty,
          code: tenant.code,
          legalNameEn: tenant.legalNameEn,
          legalNameAr: tenant.legalNameAr,
          countryCode: tenant.countryCode ?? '',
          defaultCurrency: tenant.defaultCurrency ?? '',
          tier: String(tenant.tier),
          externalIdentifier: tenant.externalIdentifier ?? '',
        };
  });

  protected readonly creating = computed(() => this.tenant() === null);
  protected readonly form = form(this.model, (path) => {
    required(path.code, { message: wording('10502') });
    readonly(path.code, { when: () => !this.creating() });
    pattern(path.code, tenantCode, { message: wording('10503') });
    required(path.legalNameEn, { message: wording('10504') });
    required(path.legalNameAr, { message: wording('10506') });
    required(path.countryCode, { message: wording('10508') });
    required(path.defaultCurrency, { message: wording('10510') });
    required(path.administratorEmail, {
      message: wording('10516'),
      when: () => this.creating(),
    });
    email(path.administratorEmail, { message: wording('10517') });
    maxLength(path.administratorEmail, EMAIL_MAX_LENGTH, { message: wording('10518') });
    required(path.administratorDisplayName, {
      message: wording('10519'),
      when: () => this.creating(),
    });
    maxLength(path.administratorDisplayName, NAME_MAX_LENGTH, { message: wording('10520') });
  });
  protected readonly problem = signal<string | null>(null);
  /** True once a submit was tried: a refusal on the read-only code shows without a touch. */
  protected readonly submitted = signal(false);

  constructor() {
    this.countries.load();
    this.currencies.load();
    afterNextRender(() => {
      this.firstBox().nativeElement.focus();
    });
  }

  protected label(row: ReferenceRow): string {
    return String(row['nameEn'] ?? row['code'] ?? '');
  }

  protected code(row: ReferenceRow): string {
    return String(row['code'] ?? '');
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    this.submitted.set(true);
    void submit(this.form, async (field) => {
      this.problem.set(null);
      const value = field().value();
      const draft: TenantDraft = {
        ...value,
        code: value.code.trim(),
        legalNameEn: value.legalNameEn.trim(),
        legalNameAr: value.legalNameAr.trim(),
        externalIdentifier: value.externalIdentifier.trim(),
        administratorEmail: value.administratorEmail.trim(),
        administratorDisplayName: value.administratorDisplayName.trim(),
        tier: Number(value.tier),
      };
      const tenant = this.tenant();
      const result =
        tenant === null
          ? await this.store.create(draft, newIntentKey())
          : await this.store.update(tenant, draft, newIntentKey());
      return this.settle(result, field);
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
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
