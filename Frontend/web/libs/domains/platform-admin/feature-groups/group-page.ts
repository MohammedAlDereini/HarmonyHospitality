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
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormField, form, pattern, required, submit } from '@angular/forms/signals';
import { GroupsStore, ReferenceStore } from '@harmony/platform-admin/data-access';
import {
  BrandDetail,
  GroupMember,
  GroupUpdate,
  Property,
  PropertyGroup,
  ReferenceRow,
  groupTitle,
  isBrandType,
  propertyTitle,
} from '@harmony/platform-admin/domain';
import { TenantChoicesStore } from '@harmony/platform-operations/data-access';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { Icon } from '@harmony/ui/icons';
import { ConfirmDialog, Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead, PopAt } from '@harmony/ui/shell';

// A whole number, 0 or more. Matches "0" and "120"; rejects "-1" and "12a".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5B0-9%5D%2B%5Cs*%24&testString=120
const wholeNumber = /^\s*[0-9]+\s*$/;
// A percentage with up to two decimals. Matches "5.5"; rejects "5.555" and "x".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5B0-9%5D%7B1%2C3%7D(%5C.%5B0-9%5D%7B1%2C2%7D)%3F%5Cs*%24&testString=5.5
const percent = /^\s*[0-9]{1,3}(\.[0-9]{1,2})?\s*$/;
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

/** The page's tabs (C15); Brand only when the group is one. */
export type GroupTab = 'overview' | 'hotels' | 'brand' | 'standards';
const tabs: readonly GroupTab[] = ['overview', 'hotels', 'brand', 'standards'];
const isTab = (value: string): value is GroupTab => (tabs as readonly string[]).includes(value);
const dateFormat: Intl.DateTimeFormatOptions = {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
};

/** A reference row's text under a key; '' when the row has none. */
const textOf = (row: ReferenceRow, key: string): string => {
  const value = row[key];
  return typeof value === 'string' ? value : '';
};

/** "01 Jan 2024 – 31 Dec 2028" (each day through `day`), or '' when the brand has no agreement dates. */
const agreementOf = (brand: BrandDetail, day: (iso: string) => string): string =>
  brand.agreementStartsOn === null && brand.agreementEndsOn === null
    ? ''
    : `${brand.agreementStartsOn === null ? '…' : day(brand.agreementStartsOn)} – ${brand.agreementEndsOn === null ? '…' : day(brand.agreementEndsOn)}`;

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
 * One hotel group (platform console design, C15): the head says what it is, whose it is and how many hotels carry it;
 * the overview holds the brand's own columns when it is a brand, what the group is about, and the standards the
 * design proposes (not built, said so); the hotels in it with their periods follow: join one in a drawer, end one
 * after a confirm. The part the console may change (never the code or the type) is edited in the drawer.
 */
@Component({
  selector: 'hr-group-page',
  imports: [
    NgTemplateOutlet,
    RouterLink,
    FormField,
    IfAllowed,
    Icon,
    PageHead,
    PopAt,
    Drawer,
    ConfirmDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './group-page.html',
  styleUrl: './group-page.css',
})
export class GroupPage {
  /** From the route (:id), bound by the router. */
  readonly id = input.required<string>();
  /** ?tab=: the tab to open on. */
  readonly tab = input<string>();

  private readonly store = inject(GroupsStore);
  private readonly reference = inject(ReferenceStore);
  private readonly tenants = inject(TenantChoicesStore);
  private readonly toasts = inject(Toasts);
  private readonly editorDrawer = viewChild.required<Drawer>('editorDrawer');
  private readonly memberDrawer = viewChild.required<Drawer>('memberDrawer');
  private readonly endAsk = viewChild.required<ConfirmDialog>('endAsk');
  private readonly locale = $localize.locale ?? 'en';
  private readonly dates = new Intl.DateTimeFormat(
    this.locale === 'en' ? 'en-GB' : this.locale,
    dateFormat,
  );
  private readonly cities = this.reference.choices('City');
  private readonly types = this.reference.choices('PropertyGroupType');

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly group = signal<PropertyGroup | null>(null);
  protected readonly loading = signal(true);
  protected readonly problem = signal<string | null>(null);
  protected readonly current = signal<GroupTab>('overview');
  protected readonly editing = signal(false);
  protected readonly editProblem = signal<string | null>(null);
  protected readonly joining = signal(false);
  protected readonly joinProblem = signal<string | null>(null);
  /** The customer's hotels: the cities of the members, and what the Add-hotel drawer offers. */
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
  protected readonly past = computed(
    () => (this.group()?.members ?? []).filter((member) => !member.isCurrent).length,
  );

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
    this.cities.load();
    this.types.load();
    effect(() => {
      const id = this.id();
      untracked(() => {
        void this.read(id);
      });
    });
    effect(() => {
      const wanted = this.tab();
      untracked(() => {
        if (wanted !== undefined && isTab(wanted)) this.current.set(wanted);
      });
    });
  }

  // ── the head ────────────────────────────────────────────────────────────────

  protected name(group: PropertyGroup): string {
    return groupTitle(group, this.locale);
  }

  /** "Levant Hotels Group (LEV)": the customer the group belongs to. */
  protected tenantLabel(group: PropertyGroup): string {
    const row = this.tenants.rows().find((candidate) => candidate.id === group.tenantId);
    if (row === undefined) return '';
    const name =
      this.locale.startsWith('ar') && row.legalNameAr !== '' ? row.legalNameAr : row.legalNameEn;
    return `${name} (${row.code})`;
  }

  protected typeName(group: PropertyGroup): string {
    const row = this.types.rows().find((candidate) => textOf(candidate, 'code') === group.typeCode);
    return row === undefined ? group.typeCode : this.referenceName(row);
  }

  protected stateWord(group: PropertyGroup): string {
    return group.isActive
      ? $localize`:@@platform-admin.group.active:Active`
      : $localize`:@@platform-admin.group.inactive:Inactive`;
  }

  protected agreement(brand: BrandDetail): string {
    return agreementOf(brand, (iso) => this.dates.format(new Date(`${iso}T00:00:00Z`)));
  }

  protected show(tab: GroupTab): void {
    this.current.set(tab);
  }

  // ── the hotels ──────────────────────────────────────────────────────────────

  protected hotelName(member: GroupMember): string {
    return propertyTitle(
      { nameEn: member.propertyNameEn, nameAr: member.propertyNameAr },
      this.locale,
    );
  }

  /** The member's city, from the customer's hotels; '' until they are read. */
  protected cityOf(member: GroupMember): string {
    const hotel = this.hotels().find((candidate) => candidate.id === member.propertyId);
    if (hotel === undefined) return '';
    const row = this.cities.rows().find((candidate) => textOf(candidate, 'id') === hotel.cityId);
    return row === undefined ? '' : this.referenceName(row);
  }

  protected hotelLabel(hotel: Property): string {
    return `${hotel.code} · ${propertyTitle(hotel, this.locale)}`;
  }

  protected day(instant: string | null): string {
    return instant === null ? '—' : this.dates.format(new Date(instant));
  }

  // ── the doors ───────────────────────────────────────────────────────────────

  protected reload(): void {
    void this.read(this.id());
  }

  protected openEditor(): void {
    this.editProblem.set(null);
    this.editing.set(true);
    this.editorDrawer().open();
  }

  protected openJoin(): void {
    this.joinProblem.set(null);
    this.chosenHotel.set('');
    this.joining.set(true);
    this.memberDrawer().open();
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
      this.hotels.set(await this.store.hotelsOf(result.data.tenantId));
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

  private referenceName(row: ReferenceRow): string {
    const arabic = textOf(row, 'nameAr');
    return this.locale.startsWith('ar') && arabic !== '' ? arabic : textOf(row, 'nameEn');
  }
}
