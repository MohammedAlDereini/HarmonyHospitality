import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormField, form, pattern, required, submit } from '@angular/forms/signals';
import { GroupsStore } from '@harmony/platform-admin/data-access';
import {
  GroupMember,
  GroupUpdate,
  Property,
  PropertyGroup,
  groupTitle,
  isBrandType,
  propertyTitle,
} from '@harmony/platform-admin/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { ConfirmDialog, Drawer, Toasts } from '@harmony/ui/overlays';

// A whole number, 0 or more. Matches "0" and "120"; rejects "-1" and "12a".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5B0-9%5D%2B%5Cs*%24&testString=120
const wholeNumber = /^\s*[0-9]+\s*$/;
// A percentage with up to two decimals. Matches "5.5"; rejects "5.555" and "x".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5B0-9%5D%7B1%2C3%7D(%5C.%5B0-9%5D%7B1%2C2%7D)%3F%5Cs*%24&testString=5.5
const percent = /^\s*[0-9]{1,3}(\.[0-9]{1,2})?\s*$/;
/** The calendar day of an ISO instant: "2026-10-26T08:00:00+00:00" → "2026-10-26". */
const DAY_LENGTH = 10;
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

/** What the edit form holds; numbers travel as text until sent. */
interface UpdateValues {
  readonly nameEn: string;
  readonly nameAr: string;
  readonly description: string;
  readonly precedence: string;
  readonly displayOrder: string;
  readonly externalIdentifier: string;
  readonly isActive: boolean;
  readonly themeReference: string;
  readonly websiteUrl: string;
  readonly franchisorName: string;
  readonly agreementStartsOn: string;
  readonly agreementEndsOn: string;
  readonly royaltyPercent: string;
}

/** The text fields a server code can land on (never the flag). */
type TextField = Exclude<keyof UpdateValues, 'isActive'>;

const fieldOfCode: Readonly<Record<string, TextField>> = {
  '10704': 'nameEn',
  '10705': 'nameEn',
  '10706': 'nameAr',
  '10707': 'nameAr',
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

const empty: UpdateValues = {
  nameEn: '',
  nameAr: '',
  description: '',
  precedence: '0',
  displayOrder: '0',
  externalIdentifier: '',
  isActive: true,
  themeReference: '',
  websiteUrl: '',
  franchisorName: '',
  agreementStartsOn: '',
  agreementEndsOn: '',
  royaltyPercent: '',
};

/**
 * One hotel group: what it is, the brand's own columns when it is a brand, the part the console may change (never
 * the code or the type), and the hotels in it with their periods: join one in a drawer, end one after a confirm
 * (architecture 6, platform-admin).
 */
@Component({
  selector: 'hr-group-page',
  imports: [FormField, IfAllowed, Drawer, ConfirmDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './group-page.html',
  styleUrl: './group-page.css',
})
export class GroupPage {
  /** From the route (:id), bound by the router. */
  readonly id = input.required<string>();

  private readonly store = inject(GroupsStore);
  private readonly toasts = inject(Toasts);
  private readonly editorDrawer = viewChild.required<Drawer>('editorDrawer');
  private readonly memberDrawer = viewChild.required<Drawer>('memberDrawer');
  private readonly endAsk = viewChild.required<ConfirmDialog>('endAsk');
  private readonly locale = $localize.locale ?? 'en';

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly group = signal<PropertyGroup | null>(null);
  protected readonly loading = signal(true);
  protected readonly problem = signal<string | null>(null);
  protected readonly editing = signal(false);
  protected readonly editProblem = signal<string | null>(null);
  protected readonly joining = signal(false);
  protected readonly joinProblem = signal<string | null>(null);
  protected readonly hotels = signal<readonly Property[]>([]);
  protected readonly chosenHotel = signal('');
  protected readonly isBrand = computed(() => {
    const group = this.group();
    return group !== null && isBrandType(group.typeCode);
  });
  /** The hotels not in the group today: what the Add-hotel drawer offers. */
  protected readonly joinable = computed(() => {
    const current = new Set(
      (this.group()?.members ?? []).filter((m) => m.isCurrent).map((m) => m.propertyId),
    );
    return this.hotels().filter((hotel) => !current.has(hotel.id));
  });

  /** The editable copy of what may change; a fresh group resets it. */
  private readonly model = linkedSignal<UpdateValues>(() => {
    const group = this.group();
    if (group === null) return empty;
    const brand = group.brand;
    const royalty = brand?.royaltyPercent ?? null;
    return {
      nameEn: group.nameEn,
      nameAr: group.nameAr,
      description: group.description ?? '',
      precedence: String(group.precedence),
      displayOrder: String(group.displayOrder),
      externalIdentifier: group.externalIdentifier ?? '',
      isActive: group.isActive,
      themeReference: brand?.themeReference ?? '',
      websiteUrl: brand?.websiteUrl ?? '',
      franchisorName: brand?.franchisorName ?? '',
      agreementStartsOn: brand?.agreementStartsOn ?? '',
      agreementEndsOn: brand?.agreementEndsOn ?? '',
      royaltyPercent: royalty === null ? '' : String(royalty),
    };
  });
  protected readonly form = form(this.model, (path) => {
    required(path.nameEn, { message: wording('10704') });
    required(path.nameAr, { message: wording('10706') });
    required(path.precedence, { message: wording('10711') });
    pattern(path.precedence, wholeNumber, { message: wording('10711') });
    required(path.displayOrder, { message: wording('10712') });
    pattern(path.displayOrder, wholeNumber, { message: wording('10712') });
    pattern(path.royaltyPercent, percent, { message: wording('10719') });
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => {
        void this.read(id);
      });
    });
  }

  protected name(group: PropertyGroup): string {
    return groupTitle(group, this.locale);
  }

  protected hotelName(member: GroupMember): string {
    return propertyTitle(
      { nameEn: member.propertyNameEn, nameAr: member.propertyNameAr },
      this.locale,
    );
  }

  protected hotelLabel(hotel: Property): string {
    return `${hotel.code} · ${propertyTitle(hotel, this.locale)}`;
  }

  protected day(instant: string): string {
    return instant.slice(0, DAY_LENGTH);
  }

  protected reload(): void {
    void this.read(this.id());
  }

  protected openEditor(): void {
    this.editProblem.set(null);
    this.editing.set(true);
    this.editorDrawer().open();
  }

  protected async openJoin(group: PropertyGroup): Promise<void> {
    this.joinProblem.set(null);
    this.chosenHotel.set('');
    this.joining.set(true);
    this.memberDrawer().open();
    this.hotels.set(await this.store.hotelsOf(group.tenantId));
  }

  protected onSave(event: Event, group: PropertyGroup): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.editProblem.set(null);
      const value = field().value();
      const update: GroupUpdate = {
        nameEn: value.nameEn.trim(),
        nameAr: value.nameAr.trim(),
        description: value.description,
        precedence: Number(value.precedence.trim()),
        displayOrder: Number(value.displayOrder.trim()),
        externalIdentifier: value.externalIdentifier,
        isActive: value.isActive,
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
      const result = await this.store.update(group, update, newIntentKey());
      return this.settle(result, field);
    });
  }

  protected async onJoin(event: Event, group: PropertyGroup): Promise<void> {
    event.preventDefault();
    const propertyId = this.chosenHotel();
    if (propertyId === '') return;
    this.joinProblem.set(null);
    const result = await this.store.addMember(group.id, propertyId, newIntentKey());
    if (result.kind === 'ok') {
      this.toasts.show($localize`:@@platform-admin.group.toast.joined:Hotel added to the group.`);
      this.memberDrawer().close();
      this.reload();
      return;
    }
    this.joinProblem.set(messageOf(result));
  }

  protected async onEnd(member: GroupMember): Promise<void> {
    if (!(await this.endAsk().ask())) return;
    const result = await this.store.endMembership(member.id, newIntentKey());
    if (result.kind === 'ok') {
      this.toasts.show($localize`:@@platform-admin.group.toast.ended:Membership ended.`);
      this.reload();
      return;
    }
    this.toasts.show(messageOf(result), true);
  }

  private async read(id: string): Promise<void> {
    this.loading.set(true);
    const result = await this.store.detail(id);
    if (result.kind === 'ok') {
      this.group.set(result.data);
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
      this.toasts.show($localize`:@@platform-admin.group.toast.saved:Group saved.`);
      this.editorDrawer().close();
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
