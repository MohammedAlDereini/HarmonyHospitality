import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormField, email, form, maxLength, pattern, required } from '@angular/forms/signals';
import { PropertiesStore, ReferenceStore, TenantsStore } from '@harmony/platform-admin/data-access';
import {
  PropertyDraft,
  PropertyLine,
  ReferenceRow,
  TENANT_MAX_TIER,
  TENANT_MIN_TIER,
  TenantDraft,
} from '@harmony/platform-admin/domain';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { Icon } from '@harmony/ui/icons';
import { ConfirmDialog, Toasts } from '@harmony/ui/overlays';
import { PageHead } from '@harmony/ui/shell';

/** The five steps, in order. */
export type WizardStep = 'company' | 'administrator' | 'hotel' | 'modules' | 'review';
const steps: readonly WizardStep[] = ['company', 'administrator', 'hotel', 'modules', 'review'];
const stepTitle: Readonly<Record<WizardStep, string>> = {
  company: $localize`:@@platform-admin.wizard.step.company:Company`,
  administrator: $localize`:@@platform-admin.wizard.step.administrator:Administrator`,
  hotel: $localize`:@@platform-admin.wizard.step.hotel:First hotel`,
  modules: $localize`:@@platform-admin.wizard.step.modules:Modules`,
  review: $localize`:@@platform-admin.wizard.step.review:Review and finish`,
};

const NAME_MAX_LENGTH = 128;
const EMAIL_MAX_LENGTH = 256;
// A code as Identity accepts it: 2 to 32 letters, digits, dots, dashes or underscores, with the surrounding spaces
// the form trims. Matches " blue-nile "; rejects "b" (too short) and "blue nile" (a space inside).
// Tester: https://regex101.com/?regex=%5E%5Cs*%5BA-Za-z0-9._-%5D%7B2%2C32%7D%5Cs*%24&testString=%20blue-nile%20
const codeText = /^\s*[A-Za-z0-9._-]{2,32}\s*$/;
// A whole number of rooms, 1 or more. Matches "120"; rejects "0" and "12a".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5B1-9%5D%5B0-9%5D*%5Cs*%24&testString=120
const roomsText = /^\s*[1-9][0-9]*\s*$/;
const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');
const tiers: readonly string[] = Array.from(
  { length: TENANT_MAX_TIER - TENANT_MIN_TIER + 1 },
  (_, index) => String(TENANT_MIN_TIER + index),
);
const dateFormat: Intl.DateTimeFormatOptions = {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
};

/** Everything the wizard asks, as the fields carry it (numbers and the tier as text). */
interface Draft {
  readonly code: string;
  readonly legalNameEn: string;
  readonly legalNameAr: string;
  readonly countryCode: string;
  readonly defaultCurrency: string;
  readonly tier: string;
  readonly externalIdentifier: string;
  readonly administratorEmail: string;
  readonly administratorDisplayName: string;
  readonly hotelWanted: boolean;
  readonly hotelCode: string;
  readonly hotelExternal: string;
  readonly hotelNameEn: string;
  readonly hotelNameAr: string;
  readonly propertyTypeCode: string;
  readonly cityId: string;
  readonly addressLine: string;
  readonly postalCode: string;
  readonly hotelCurrency: string;
  readonly defaultLanguageCode: string;
  readonly totalRooms: string;
  readonly opensOn: string;
  readonly legalEnvironmentCode: string;
  readonly taxEnvironmentCode: string;
  readonly accountingEnvironmentCode: string;
  readonly accommodationEnvironmentCode: string;
  readonly startsOn: string;
}

/** One module line being typed for the hotel, before it joins the list. */
interface LineDraft {
  readonly moduleCode: string;
  readonly rooms: string;
  readonly terminals: string;
  readonly renewsAutomatically: boolean;
}

const emptyDraft: Draft = {
  code: '',
  legalNameEn: '',
  legalNameAr: '',
  countryCode: '',
  defaultCurrency: '',
  tier: String(TENANT_MIN_TIER),
  externalIdentifier: '',
  administratorEmail: '',
  administratorDisplayName: '',
  hotelWanted: false,
  hotelCode: '',
  hotelExternal: '',
  hotelNameEn: '',
  hotelNameAr: '',
  propertyTypeCode: '',
  cityId: '',
  addressLine: '',
  postalCode: '',
  hotelCurrency: '',
  defaultLanguageCode: '',
  totalRooms: '',
  opensOn: '',
  legalEnvironmentCode: '',
  taxEnvironmentCode: '',
  accountingEnvironmentCode: '',
  accommodationEnvironmentCode: '',
  startsOn: '',
};
// A whole number: digits only. Matches "60"; rejects "-5" and "6.5".
// Tester: https://regex101.com/?regex=%5E%5Cd%2B%24&testString=60
const wholeNumberText = /^\d+$/;

const emptyLine: LineDraft = {
  moduleCode: '',
  rooms: '',
  terminals: '',
  renewsAutomatically: true,
};

const textOf = (row: ReferenceRow, key: string): string => {
  const value = row[key];
  return typeof value === 'string' ? value : '';
};

/**
 * A new customer in five steps (platform console design, C5): the company, its first administrator, an optional
 * first hotel with its module lines, then a review. Nothing is saved until Finish, which sends Tenant/Create (with the
 * administrator, who is invited by mail) and then, when a hotel was given, Property/Create with the lines. The draft
 * lives in this tab only.
 */
@Component({
  selector: 'hr-new-tenant-page',
  imports: [NgTemplateOutlet, RouterLink, FormField, Icon, PageHead, ConfirmDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './new-tenant-page.html',
  styleUrl: './new-tenant-page.css',
})
export class NewTenantPage {
  private readonly tenants = inject(TenantsStore);
  private readonly properties = inject(PropertiesStore);
  private readonly reference = inject(ReferenceStore);
  private readonly toasts = inject(Toasts);
  private readonly router = inject(Router);
  private readonly discardAsk = viewChild.required(ConfirmDialog);
  private readonly locale = $localize.locale ?? 'en';
  private readonly dates = new Intl.DateTimeFormat(
    this.locale === 'en' ? 'en-GB' : this.locale,
    dateFormat,
  );

  protected readonly countries = this.reference.choices('Country');
  protected readonly currencies = this.reference.choices('Currency');
  protected readonly types = this.reference.choices('PropertyType');
  protected readonly cities = this.reference.choices('City');
  protected readonly languages = this.reference.choices('Language');
  protected readonly environments = this.reference.choices('RegulatoryEnvironment');
  protected readonly modules = this.reference.choices('Module');
  protected readonly legal = computed(() => this.ofKind('Legal'));
  protected readonly tax = computed(() => this.ofKind('Tax'));
  protected readonly accounting = computed(() => this.ofKind('Accounting'));
  protected readonly accommodation = computed(() => this.ofKind('Accommodation'));
  protected readonly tiers = tiers;
  protected readonly steps = steps;
  protected readonly stepTitle = stepTitle;

  protected readonly model = signal<Draft>(emptyDraft);
  protected readonly form = form(this.model, (path) => {
    required(path.code, { message: wording('10502') });
    pattern(path.code, codeText, { message: wording('10503') });
    required(path.legalNameEn, { message: wording('10504') });
    required(path.legalNameAr, { message: wording('10506') });
    required(path.countryCode, { message: wording('10508') });
    required(path.defaultCurrency, { message: wording('10510') });
    required(path.administratorEmail, { message: wording('10516') });
    email(path.administratorEmail, { message: wording('10517') });
    maxLength(path.administratorEmail, EMAIL_MAX_LENGTH, { message: wording('10518') });
    required(path.administratorDisplayName, { message: wording('10519') });
    maxLength(path.administratorDisplayName, NAME_MAX_LENGTH, { message: wording('10520') });
    const hotel = (): boolean => this.model().hotelWanted;
    required(path.hotelCode, { message: wording('10601'), when: hotel });
    pattern(path.hotelCode, codeText, { message: wording('10602') });
    required(path.hotelNameEn, { message: wording('10603'), when: hotel });
    required(path.hotelNameAr, { message: wording('10605'), when: hotel });
    required(path.propertyTypeCode, { message: wording('10607'), when: hotel });
    required(path.cityId, { message: wording('10608'), when: hotel });
    required(path.addressLine, { message: wording('10621'), when: hotel });
    required(path.hotelCurrency, { message: wording('10510'), when: hotel });
    required(path.defaultLanguageCode, {
      message: $localize`:@@platform-admin.new-tenant.language-required:Choose a language.`,
      when: hotel,
    });
    required(path.totalRooms, {
      message: $localize`:@@platform-admin.new-tenant.rooms-required:Give the number of rooms.`,
      when: hotel,
    });
    pattern(path.totalRooms, roomsText, {
      message: $localize`:@@platform-admin.rooms.whole-number:Give the rooms as a whole number, 1 or more.`,
    });
    required(path.legalEnvironmentCode, { message: wording('10612'), when: hotel });
    required(path.taxEnvironmentCode, { message: wording('10612'), when: hotel });
    required(path.accountingEnvironmentCode, { message: wording('10612'), when: hotel });
    required(path.accommodationEnvironmentCode, { message: wording('10612'), when: hotel });
    required(path.startsOn, {
      message: $localize`:@@platform-admin.properties.starts-on.required:Give the start date.`,
      when: hotel,
    });
  });
  protected readonly lines = signal<readonly PropertyLine[]>([]);
  protected readonly line = signal<LineDraft>(emptyLine);
  protected readonly current = signal<WizardStep>('company');
  protected readonly finishing = signal(false);
  protected readonly problem = signal<string | null>(null);
  /** Set when the tenant was made but its hotel was refused: the tenant exists, the hotel does not. */
  protected readonly tenantMadeId = signal<string | null>(null);
  protected readonly index = computed(() => steps.indexOf(this.current()));
  protected readonly lineCodes = computed(() =>
    this.lines()
      .map((line) => line.moduleCode)
      .join(' · '),
  );
  protected readonly title = computed(() => {
    const name = this.model().legalNameEn.trim();
    return name === ''
      ? $localize`:@@platform-admin.wizard.title:New tenant`
      : $localize`:@@platform-admin.wizard.title.named:New tenant · ${name}:NAME:`;
  });

  constructor() {
    this.countries.load();
    this.currencies.load();
    this.types.load();
    this.cities.load();
    this.languages.load();
    this.environments.load();
    this.modules.load();
  }

  // ── the steps ───────────────────────────────────────────────────────────────

  protected stateOf(step: WizardStep): 'done' | 'current' | 'todo' {
    const at = steps.indexOf(step);
    if (at === this.index()) return 'current';
    return at < this.index() ? 'done' : 'todo';
  }

  protected go(step: WizardStep): void {
    if (steps.indexOf(step) < this.index()) this.current.set(step);
  }

  /** The first regime still unchosen once the person tried to move on: one line under the four selects. */
  protected readonly regimeError = computed(() => {
    const states = [
      this.form.legalEnvironmentCode(),
      this.form.taxEnvironmentCode(),
      this.form.accountingEnvironmentCode(),
      this.form.accommodationEnvironmentCode(),
    ];
    const failing = states.find((state) => state.touched() && state.errors().length > 0);
    return failing?.errors()[0]?.message ?? null;
  });

  /** Rooms and terminals on a line are whole numbers, zero or more, or left empty; a module gets one line. */
  protected readonly lineProblem = computed(() => {
    const line = this.line();
    const bad = (text: string) => text.trim() !== '' && !wholeNumberText.test(text.trim());
    if (bad(line.rooms) || bad(line.terminals))
      return $localize`:@@platform-admin.wizard.line.whole-number:Rooms and terminals are whole numbers, zero or more.`;
    if (line.moduleCode !== '' && this.lines().some((l) => l.moduleCode === line.moduleCode))
      return $localize`:@@platform-admin.wizard.line.duplicate:That module already has a line below.`;
    return null;
  });

  protected next(): void {
    if (!this.stepValid(this.current())) {
      this.touch(this.current());
      return;
    }
    const at = this.index();
    const following = steps[at + 1];
    if (following !== undefined) this.current.set(following);
  }

  protected back(): void {
    const at = this.index();
    const previous = steps[at - 1];
    if (previous !== undefined) this.current.set(previous);
  }

  /** The fields of a step; the hotel's only count when a hotel is wanted. */
  protected stepValid(step: WizardStep): boolean {
    return this.fieldsOf(step).every((field) => field().valid());
  }

  protected wantHotel(wanted: boolean): void {
    this.model.update((draft) => ({ ...draft, hotelWanted: wanted }));
  }

  // ── the lines ───────────────────────────────────────────────────────────────

  protected lineChange(key: keyof LineDraft, value: string | boolean): void {
    this.line.update((line) => ({ ...line, [key]: value }));
  }

  protected addLine(): void {
    const draft = this.line();
    if (this.lineProblem() !== null) return;
    if (draft.moduleCode === '' || this.lines().some((l) => l.moduleCode === draft.moduleCode))
      return;
    this.lines.update((lines) => [
      ...lines,
      {
        moduleCode: draft.moduleCode,
        rooms: draft.rooms.trim() === '' ? null : Number(draft.rooms.trim()),
        terminals: draft.terminals.trim() === '' ? null : Number(draft.terminals.trim()),
        renewsAutomatically: draft.renewsAutomatically,
      },
    ]);
    this.line.set(emptyLine);
  }

  protected removeLine(moduleCode: string): void {
    this.lines.update((lines) => lines.filter((line) => line.moduleCode !== moduleCode));
  }

  // ── names and facts ─────────────────────────────────────────────────────────

  protected code(row: ReferenceRow): string {
    return textOf(row, 'code') === '' ? textOf(row, 'id') : textOf(row, 'code');
  }

  protected label(row: ReferenceRow): string {
    const arabic = textOf(row, 'nameAr');
    return this.locale.startsWith('ar') && arabic !== '' ? arabic : textOf(row, 'nameEn');
  }

  protected moduleName(moduleCode: string): string {
    const row = this.modules.rows().find((candidate) => textOf(candidate, 'code') === moduleCode);
    return row === undefined ? moduleCode : `${moduleCode} · ${this.label(row)}`;
  }

  protected countryName(): string {
    const row = this.countries
      .rows()
      .find((candidate) => textOf(candidate, 'code') === this.model().countryCode);
    return row === undefined ? this.model().countryCode : this.label(row);
  }

  protected cityName(): string {
    const row = this.cities
      .rows()
      .find((candidate) => textOf(candidate, 'id') === this.model().cityId);
    return row === undefined ? '' : this.label(row);
  }

  protected day(date: string): string {
    return date === '' ? '' : this.dates.format(new Date(`${date}T00:00:00Z`));
  }

  // ── finish ──────────────────────────────────────────────────────────────────

  protected finish(): void {
    void this.finishNow();
  }

  protected async discard(): Promise<void> {
    if (!(await this.discardAsk().ask())) return;
    await this.router.navigateByUrl('/tenants');
  }

  private fieldsOf(
    step: WizardStep,
  ): readonly (() => { valid(): boolean; markAsTouched(): void })[] {
    const f = this.form;
    const byStep: Readonly<
      Record<WizardStep, readonly (() => { valid(): boolean; markAsTouched(): void })[]>
    > = {
      company: [
        f.code,
        f.legalNameEn,
        f.legalNameAr,
        f.countryCode,
        f.defaultCurrency,
        f.tier,
        f.externalIdentifier,
      ],
      administrator: [f.administratorEmail, f.administratorDisplayName],
      hotel: this.model().hotelWanted
        ? [
            f.hotelCode,
            f.hotelNameEn,
            f.hotelNameAr,
            f.propertyTypeCode,
            f.cityId,
            f.addressLine,
            f.postalCode,
            f.hotelCurrency,
            f.defaultLanguageCode,
            f.totalRooms,
            f.opensOn,
            f.legalEnvironmentCode,
            f.taxEnvironmentCode,
            f.accountingEnvironmentCode,
            f.accommodationEnvironmentCode,
            f.startsOn,
          ]
        : [],
      modules: [],
      review: [],
    };
    return byStep[step];
  }

  private touch(step: WizardStep): void {
    this.fieldsOf(step).forEach((field) => {
      field().markAsTouched();
    });
  }

  private ofKind(kind: string): readonly ReferenceRow[] {
    return this.environments.rows().filter((row) => row['kind'] === kind);
  }

  private tenantDraft(): TenantDraft {
    const value = this.model();
    return {
      code: value.code.trim(),
      legalNameEn: value.legalNameEn.trim(),
      legalNameAr: value.legalNameAr.trim(),
      countryCode: value.countryCode,
      defaultCurrency: value.defaultCurrency,
      tier: Number(value.tier),
      externalIdentifier: value.externalIdentifier.trim(),
      administratorEmail: value.administratorEmail.trim(),
      administratorDisplayName: value.administratorDisplayName.trim(),
    };
  }

  private hotelDraft(tenantId: string): PropertyDraft {
    const value = this.model();
    return {
      tenantId,
      code: value.hotelCode.trim(),
      nameEn: value.hotelNameEn.trim(),
      nameAr: value.hotelNameAr.trim(),
      propertyTypeCode: value.propertyTypeCode,
      cityId: value.cityId,
      addressLine: value.addressLine.trim(),
      postalCode: value.postalCode.trim(),
      defaultCurrency: value.hotelCurrency,
      defaultLanguageCode: value.defaultLanguageCode,
      totalRooms: Number(value.totalRooms.trim()),
      legalEnvironmentCode: value.legalEnvironmentCode,
      taxEnvironmentCode: value.taxEnvironmentCode,
      accountingEnvironmentCode: value.accountingEnvironmentCode,
      accommodationEnvironmentCode: value.accommodationEnvironmentCode,
      externalIdentifier: value.hotelExternal.trim(),
      startsOn: value.startsOn,
      lines: this.lines(),
    };
  }

  /** Tenant/Create first; the id it answers names the hotel's Property/Create. A refused hotel leaves the tenant. */
  private async finishNow(): Promise<void> {
    if (!steps.every((step) => this.stepValid(step))) {
      steps.forEach((step) => {
        this.touch(step);
      });
      return;
    }
    this.problem.set(null);
    this.finishing.set(true);
    const made = await this.tenants.create(this.tenantDraft(), newIntentKey());
    if (made.kind !== 'ok') {
      this.finishing.set(false);
      this.problem.set(messageOf(made));
      return;
    }
    const tenantId = typeof made.data === 'string' ? made.data : '';
    if (this.model().hotelWanted && tenantId !== '') {
      const hotel: ApiResult<unknown> = await this.properties.create(
        this.hotelDraft(tenantId),
        newIntentKey(),
      );
      if (hotel.kind !== 'ok') {
        this.finishing.set(false);
        this.tenantMadeId.set(tenantId);
        this.problem.set(
          $localize`:@@platform-admin.wizard.hotel-refused:The tenant was created and the invitation sent, but the hotel was refused: ${messageOf(hotel)}:REASON:`,
        );
        return;
      }
    }
    this.toasts.show(
      $localize`:@@platform-admin.wizard.toast.done:Tenant created. The administrator's invitation is on its way.`,
    );
    await this.router.navigateByUrl(tenantId === '' ? '/tenants' : `/tenants/${tenantId}`);
  }
}
