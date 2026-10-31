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
import { GroupsStore, ReferenceStore } from '@harmony/platform-admin/data-access';
import { TenantChoicesStore } from '@harmony/platform-operations/data-access';
import {
  GroupDraft,
  ReferenceRow,
  Tenant,
  isBrandType,
  tenantName,
} from '@harmony/platform-admin/domain';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';

// A group code as Identity accepts it: 2 to 32 letters, digits, dots, dashes or underscores, with the surrounding
// spaces the form trims. Matches " RITZ-CARLTON "; rejects "R" (too short) and "RI TZ" (a space inside).
// Tester: https://regex101.com/?regex=%5E%5Cs*%5BA-Za-z0-9._-%5D%7B2%2C32%7D%5Cs*%24&testString=%20RITZ-CARLTON%20
const groupCode = /^\s*[A-Za-z0-9._-]{2,32}\s*$/;
// A whole number, 0 or more, for precedence and display order. Matches "0" and "120"; rejects "-1" and "12a".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5B0-9%5D%2B%5Cs*%24&testString=120
const wholeNumber = /^\s*[0-9]+\s*$/;
// A percentage with up to two decimals, for the royalty. Matches "5.5" and "12.25"; rejects "5.555" and "x".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5B0-9%5D%7B1%2C3%7D(%5C.%5B0-9%5D%7B1%2C2%7D)%3F%5Cs*%24&testString=5.5
const percent = /^\s*[0-9]{1,3}(\.[0-9]{1,2})?\s*$/;
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

/** What the form holds; numbers travel as text until sent. */
interface GroupValues {
  readonly tenantId: string;
  readonly typeCode: string;
  readonly code: string;
  readonly nameEn: string;
  readonly nameAr: string;
  readonly description: string;
  readonly precedence: string;
  readonly displayOrder: string;
  readonly externalIdentifier: string;
  readonly themeReference: string;
  readonly websiteUrl: string;
  readonly franchisorName: string;
  readonly agreementStartsOn: string;
  readonly agreementEndsOn: string;
  readonly royaltyPercent: string;
}

/** The field each of Identity's codes belongs to, whatever the kind of refusal. */
const fieldOfCode: Readonly<Record<string, keyof GroupValues>> = {
  '10501': 'tenantId',
  '10702': 'code',
  '10703': 'code',
  '00097': 'code',
  '10704': 'nameEn',
  '10705': 'nameEn',
  '10706': 'nameAr',
  '10707': 'nameAr',
  '10708': 'typeCode',
  '00055': 'typeCode',
  '00098': 'typeCode',
  '10709': 'description',
  '10710': 'externalIdentifier',
  '10711': 'precedence',
  '10712': 'displayOrder',
  '10715': 'themeReference',
  '10716': 'websiteUrl',
  '10717': 'franchisorName',
  '10718': 'agreementEndsOn',
  '10719': 'royaltyPercent',
};

const empty: GroupValues = {
  tenantId: '',
  typeCode: '',
  code: '',
  nameEn: '',
  nameAr: '',
  description: '',
  precedence: '0',
  displayOrder: '0',
  externalIdentifier: '',
  themeReference: '',
  websiteUrl: '',
  franchisorName: '',
  agreementStartsOn: '',
  agreementEndsOn: '',
  royaltyPercent: '',
};

/**
 * A new hotel group for a customer (CreatePropertyGroupCommand): its customer, its type, its code and names, its
 * precedence; the brand's own columns when the type is Brand. One intent key per press; Identity's codes back under
 * their fields.
 */
@Component({
  selector: 'hr-group-editor',
  imports: [FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './group-editor.html',
  styleUrl: './group-editor.css',
})
export class GroupEditor {
  /** The tenant the group is for, when the screen already knows it. */
  readonly tenantId = input<string | null>(null);
  readonly done = output();
  readonly cancelled = output();

  private readonly store = inject(GroupsStore);
  private readonly reference = inject(ReferenceStore);
  /** Every customer by code, whatever the Tenants page is filtered to; the platform itself owns no hotel. */
  private readonly tenants = inject(TenantChoicesStore);
  protected readonly tenantChoices = computed(() =>
    this.tenants.rows().filter((tenant) => !tenant.isPlatform),
  );
  private readonly firstBox = viewChild.required<ElementRef<HTMLSelectElement>>('firstBox');
  private readonly locale = $localize.locale ?? 'en';
  protected readonly types = this.reference.choices('PropertyGroupType');

  private readonly model = signal<GroupValues>(empty);
  protected readonly form = form(this.model, (path) => {
    required(path.tenantId, {
      message: $localize`:@@platform-admin.group-editor.tenant-required:Choose a tenant.`,
    });
    required(path.typeCode, { message: wording('10708') });
    required(path.code, { message: wording('10702') });
    pattern(path.code, groupCode, { message: wording('10703') });
    required(path.nameEn, { message: wording('10704') });
    required(path.nameAr, { message: wording('10706') });
    required(path.precedence, { message: wording('10711') });
    pattern(path.precedence, wholeNumber, { message: wording('10711') });
    required(path.displayOrder, { message: wording('10712') });
    pattern(path.displayOrder, wholeNumber, { message: wording('10712') });
    pattern(path.royaltyPercent, percent, { message: wording('10719') });
  });
  protected readonly isBrand = computed(() => isBrandType(this.form.typeCode().value()));
  protected readonly problem = signal<string | null>(null);

  constructor() {
    this.types.load();
    afterNextRender(() => {
      const known = this.tenantId();
      if (known !== null) this.model.update((values) => ({ ...values, tenantId: known }));
      this.firstBox().nativeElement.focus();
    });
  }

  protected code(row: ReferenceRow): string {
    return String(row['code'] ?? '');
  }

  protected label(row: ReferenceRow): string {
    const arabic = String(row['nameAr'] ?? '');
    if (this.locale.startsWith('ar') && arabic !== '') return arabic;
    return String(row['nameEn'] ?? row['code'] ?? '');
  }

  protected tenantLabel(tenant: Pick<Tenant, 'code' | 'legalNameEn' | 'legalNameAr'>): string {
    return `${tenant.code} · ${tenantName(tenant, this.locale)}`;
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.problem.set(null);
      const value = field().value();
      const draft: GroupDraft = {
        tenantId: value.tenantId,
        typeCode: value.typeCode,
        code: value.code.trim(),
        nameEn: value.nameEn.trim(),
        nameAr: value.nameAr.trim(),
        description: value.description,
        precedence: Number(value.precedence.trim()),
        displayOrder: Number(value.displayOrder.trim()),
        externalIdentifier: value.externalIdentifier,
        brand: this.isBrand()
          ? {
              themeReference: value.themeReference,
              websiteUrl: value.websiteUrl,
              franchisorName: value.franchisorName,
              agreementStartsOn: value.agreementStartsOn,
              agreementEndsOn: value.agreementEndsOn,
              royaltyPercent:
                value.royaltyPercent.trim() === '' ? null : Number(value.royaltyPercent.trim()),
            }
          : null,
      };
      const result = await this.store.create(draft, newIntentKey());
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
