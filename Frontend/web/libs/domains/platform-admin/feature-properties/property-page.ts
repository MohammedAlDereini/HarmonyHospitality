import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  linkedSignal,
  signal,
  untracked,
} from '@angular/core';
import { FormField, form, pattern, required, submit } from '@angular/forms/signals';
import { PropertiesStore } from '@harmony/platform-admin/data-access';
import {
  Property,
  PropertyUpdate,
  SubscriptionLine,
  propertyTitle,
} from '@harmony/platform-admin/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';

// A whole number of rooms, 1 or more. Matches "120"; rejects "0" and "12a".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5B1-9%5D%5B0-9%5D*%5Cs*%24&testString=120
const roomsText = /^\s*[1-9][0-9]*\s*$/;
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

/** What the update form holds; the number of rooms and the dates travel as text until sent. */
interface UpdateValues {
  readonly nameEn: string;
  readonly nameAr: string;
  readonly addressLine: string;
  readonly postalCode: string;
  readonly totalRooms: string;
  readonly opensOn: string;
  readonly closesOn: string;
}

const fieldOfCode: Readonly<Record<string, keyof UpdateValues>> = {
  '10603': 'nameEn',
  '10604': 'nameEn',
  '10605': 'nameAr',
  '10606': 'nameAr',
  '10611': 'totalRooms',
  '10613': 'addressLine',
  '10614': 'postalCode',
};

const empty: UpdateValues = {
  nameEn: '',
  nameAr: '',
  addressLine: '',
  postalCode: '',
  totalRooms: '',
  opensOn: '',
  closesOn: '',
};

/**
 * One property: what it is and where, the part the console may change (names, address, rooms, opening and closing
 * dates), its subscription lines and contacts, read-only (architecture 6, platform-admin).
 */
@Component({
  selector: 'hr-property-page',
  imports: [FormField, IfAllowed],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './property-page.html',
  styleUrl: './property-page.css',
})
export class PropertyPage {
  /** From the route (:id), bound by the router. */
  readonly id = input.required<string>();

  private readonly store = inject(PropertiesStore);
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageProperties = Permission.ManageProperties;
  protected readonly property = signal<Property | null>(null);
  protected readonly loading = signal(true);
  protected readonly problem = signal<string | null>(null);
  protected readonly editing = signal(false);
  protected readonly editProblem = signal<string | null>(null);

  /** The editable copy of what may change; a fresh property resets it. */
  private readonly model = linkedSignal<UpdateValues>(() => {
    const property = this.property();
    return property === null
      ? empty
      : {
          nameEn: property.nameEn,
          nameAr: property.nameAr,
          addressLine: property.addressLine,
          postalCode: property.postalCode ?? '',
          totalRooms: String(property.totalRooms),
          opensOn: property.opensOn ?? '',
          closesOn: property.closesOn ?? '',
        };
  });
  protected readonly form = form(this.model, (path) => {
    required(path.nameEn, { message: wording('10603') });
    required(path.nameAr, { message: wording('10605') });
    required(path.totalRooms, { message: wording('10611') });
    pattern(path.totalRooms, roomsText, { message: wording('10611') });
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => {
        void this.read(id);
      });
    });
  }

  protected name(property: Property): string {
    return propertyTitle(property, this.locale);
  }

  protected scopeOf(line: SubscriptionLine): string {
    return line.propertyId === null
      ? $localize`:@@platform-admin.property.line.whole:Whole tenant`
      : $localize`:@@platform-admin.property.line.here:This property`;
  }

  protected reload(): void {
    void this.read(this.id());
  }

  protected openEditor(): void {
    this.editProblem.set(null);
    this.editing.update((open) => !open);
  }

  protected closeEditor(): void {
    this.editing.set(false);
  }

  protected onSave(event: Event, property: Property): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.editProblem.set(null);
      const value = field().value();
      const update: PropertyUpdate = {
        nameEn: value.nameEn.trim(),
        nameAr: value.nameAr.trim(),
        addressLine: value.addressLine.trim(),
        postalCode: value.postalCode.trim(),
        totalRooms: Number(value.totalRooms.trim()),
        opensOn: value.opensOn === '' ? null : value.opensOn,
        closesOn: value.closesOn === '' ? null : value.closesOn,
      };
      const result = await this.store.update(property, update, newIntentKey());
      return this.settle(result, field);
    });
  }

  private async read(id: string): Promise<void> {
    this.loading.set(true);
    const result = await this.store.detail(id);
    if (result.kind === 'ok') {
      this.property.set(result.data);
      this.problem.set(null);
    } else {
      this.problem.set(messageOf(result));
    }
    this.loading.set(false);
  }

  private settle(
    result: ApiResult<unknown>,
    field: typeof this.form,
  ): { kind: string; message: string; fieldTree: typeof field.nameEn }[] | undefined {
    if (result.kind === 'ok') {
      this.editing.set(false);
      this.reload();
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
    this.editProblem.set(messageOf(result));
    return undefined;
  }
}
