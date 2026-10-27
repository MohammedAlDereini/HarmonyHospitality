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
import { FormField, form, max, maxLength, min, required, submit } from '@angular/forms/signals';
import { PersonStore, RolesStore } from '@harmony/administration/data-access';
import {
  AccessScope,
  GroupChoice,
  Role,
  UserAccount,
  groupName,
  roleName,
} from '@harmony/administration/domain';
import { MyProperties, PropertySummary, propertyName } from '@harmony/platform/context';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';

/** Assign: a role at one hotel or group until ended. Grant: the same role lent for hours, with a reason. */
export type AccessMode = 'assign' | 'grant';

/** Identity's longest grant (AccessGrant.LongestWindow): 30 days, asked for in hours. */
const LONGEST_GRANT_HOURS = 720;
const SHORTEST_HOURS = 1;
const SHORTEST_DAYS = 1;
const HOURS_PER_DAY = 24;
const MS_PER_HOUR = 3_600_000;
/** Identity accepts a reason of at most this many characters (ModelValidationErrorCodes 10331). */
const REASON_MAX_LENGTH = 500;
/** A choice in the Where list: h:<hotel id> or g:<group id>. */
const hotelPrefix = 'h:';
const groupPrefix = 'g:';

interface Draft {
  readonly roleId: string;
  readonly where: string;
  /** Assign: days from now until the role ends, or empty for no end. */
  readonly days: number | null;
  /** Grant: hours from now, 1 to 720. */
  readonly hours: number | null;
  readonly reason: string;
}

const blank: Draft = { roleId: '', where: '', days: null, hours: null, reason: '' };

/** Where each of Identity's field codes lands (ModelValidationErrorCodes.Identity.UserAccount). */
const fieldOfCode: Readonly<Record<string, keyof Draft>> = {
  '10302': 'roleId',
  '10316': 'roleId',
  '10327': 'where',
  '10328': 'days',
  '10329': 'hours',
  '10334': 'hours',
  '10330': 'reason',
  '10331': 'reason',
};

const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

/** The Where choice back into Identity's scope: exactly one of the two ids. */
export const scopeOf = (where: string): AccessScope =>
  where.startsWith(groupPrefix)
    ? { propertyId: null, propertyGroupId: where.slice(groupPrefix.length) }
    : { propertyId: where.slice(hotelPrefix.length), propertyGroupId: null };

/**
 * An instant some hours from now, as Identity wants it. The device clock only names the start; Identity checks the
 * window itself (10328, 10329, 10334).
 */
const hoursFromNow = (hours: number): string =>
  new Date(Date.now() + hours * MS_PER_HOUR).toISOString();

/**
 * Gives a role at one hotel or one group (architecture 6, administration): a Signal Form, one intent key per press,
 * Identity's field codes mapped back onto the fields, every other outcome worded once. Assign is open-ended or
 * until a day; Grant is a loan of hours with a reason, 30 days at most. The hotels are the signed-in person's own,
 * the groups the customer's.
 */
@Component({
  selector: 'hr-hotel-access-editor',
  imports: [FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './hotel-access-editor.html',
  styleUrl: './hotel-access-editor.css',
})
export class HotelAccessEditor {
  readonly user = input.required<UserAccount>();
  readonly mode = input<AccessMode>('assign');
  /** The access changed; the drawer may close. */
  readonly done = output();
  /** The person gave up. */
  readonly cancelled = output();

  protected readonly roles = inject(RolesStore);
  protected readonly hotels = inject(MyProperties);
  protected readonly person = inject(PersonStore);
  private readonly roleBox = viewChild.required<ElementRef<HTMLSelectElement>>('roleBox');
  private readonly locale = $localize.locale ?? 'en';
  private readonly model = signal<Draft>(blank);

  protected readonly granting = computed(() => this.mode() === 'grant');

  protected readonly form = form(this.model, (path) => {
    required(path.roleId, { message: wording('10316') });
    required(path.where, { message: wording('10327') });
    min(path.days, SHORTEST_DAYS, { message: wording('10328') });
    required(path.hours, { message: wording('10329'), when: () => this.granting() });
    min(path.hours, SHORTEST_HOURS, { message: wording('10329') });
    max(path.hours, LONGEST_GRANT_HOURS, { message: wording('10334') });
    required(path.reason, { message: wording('10330'), when: () => this.granting() });
    maxLength(path.reason, REASON_MAX_LENGTH, { message: wording('10331') });
  });

  /** One sentence for an outcome that is not about a field, or null while all is well. */
  protected readonly problem = signal<string | null>(null);

  constructor() {
    this.roles.load();
    void this.hotels.ready();
    this.person.loadChoices();
    afterNextRender(() => {
      this.roleBox().nativeElement.focus();
    });
  }

  protected roleLabel(role: Role): string {
    return roleName(role, this.locale);
  }

  protected hotelLabel(hotel: PropertySummary): string {
    return propertyName(hotel, this.locale);
  }

  protected groupLabel(group: GroupChoice): string {
    return groupName(group, this.locale);
  }

  protected hotelChoice(hotel: PropertySummary): string {
    return `${hotelPrefix}${hotel.id}`;
  }

  protected groupChoice(group: GroupChoice): string {
    return `${groupPrefix}${group.id}`;
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.problem.set(null);
      const draft = field().value();
      const scope = scopeOf(draft.where);
      // The key is this press's own: a retry inside the call reuses it, a new press is a new intent.
      const result = this.granting()
        ? await this.person.grantAccess(
            this.user(),
            draft.roleId,
            scope,
            hoursFromNow(draft.hours ?? SHORTEST_HOURS),
            draft.reason.trim(),
            newIntentKey(),
          )
        : await this.person.assignRole(
            this.user(),
            draft.roleId,
            scope,
            draft.days === null ? null : hoursFromNow(draft.days * HOURS_PER_DAY),
            newIntentKey(),
          );
      return this.settle(result, field);
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  /** Field codes go under their fields; everything else becomes the general line; success closes the editor. */
  private settle(
    result: ApiResult<unknown>,
    field: typeof this.form,
  ): { kind: string; message: string; fieldTree: (typeof field)[keyof Draft] }[] | undefined {
    if (result.kind === 'ok') {
      this.done.emit();
      return undefined;
    }
    if (result.kind === 'invalid') {
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
