import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, form, required, submit } from '@angular/forms/signals';
import { PropertiesStore, ReferenceStore, TenantsStore } from '@harmony/platform-admin/data-access';
import {
  Property,
  ReferenceRow,
  SubscriptionDraft,
  Tenant,
  propertyTitle,
} from '@harmony/platform-admin/domain';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';

const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

/** What the form holds; numbers and dates travel as text until sent. */
interface SubscriptionValues {
  readonly moduleCode: string;
  readonly propertyId: string;
  readonly startsOn: string;
  readonly endsOn: string;
  readonly rooms: string;
  readonly terminals: string;
  readonly renewsAutomatically: boolean;
}

const empty: SubscriptionValues = {
  moduleCode: '',
  propertyId: '',
  startsOn: '',
  endsOn: '',
  rooms: '',
  terminals: '',
  renewsAutomatically: true,
};

/** A number typed as text, or null when the box was left empty. */
const numberOrNull = (text: string): number | null => (text.trim() === '' ? null : Number(text));

/**
 * One more module for a tenant, for one of its properties or for the whole tenant (SubscribeCommand): the modules
 * from the reference table, the properties from the tenant's own list, one intent key per press.
 */
@Component({
  selector: 'hr-subscription-editor',
  imports: [FormField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './subscription-editor.html',
  styleUrl: './subscription-editor.css',
})
export class SubscriptionEditor {
  readonly tenant = input.required<Tenant>();
  readonly done = output();
  readonly cancelled = output();

  private readonly store = inject(TenantsStore);
  private readonly reference = inject(ReferenceStore);
  protected readonly properties = inject(PropertiesStore);
  protected readonly modules = this.reference.choices('Module');
  private readonly moduleBox = viewChild.required<ElementRef<HTMLSelectElement>>('moduleBox');
  private readonly locale = $localize.locale ?? 'en';
  private readonly model = signal<SubscriptionValues>(empty);
  protected readonly form = form(this.model, (path) => {
    required(path.moduleCode, { message: wording('00088') });
    required(path.startsOn, {
      message: $localize`:@@platform-admin.subscriptions.starts-on.required:Give the start date.`,
    });
  });
  protected readonly problem = signal<string | null>(null);

  constructor() {
    this.modules.load();
    afterNextRender(() => {
      this.moduleBox().nativeElement.focus();
    });
  }

  protected moduleCode(row: ReferenceRow): string {
    return String(row['code'] ?? '');
  }

  protected moduleLabel(row: ReferenceRow): string {
    return `${this.moduleCode(row)} · ${String(row['nameEn'] ?? '')}`;
  }

  protected propertyLabel(property: Property): string {
    return `${property.code} · ${propertyTitle(property, this.locale)}`;
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.form, async (field) => {
      this.problem.set(null);
      const value = field().value();
      const draft: SubscriptionDraft = {
        moduleCode: value.moduleCode,
        propertyId: value.propertyId === '' ? null : value.propertyId,
        startsOn: value.startsOn,
        endsOn: value.endsOn === '' ? null : value.endsOn,
        rooms: numberOrNull(value.rooms),
        terminals: numberOrNull(value.terminals),
        renewsAutomatically: value.renewsAutomatically,
      };
      const result = await this.store.subscribe(this.tenant(), draft, newIntentKey());
      this.settle(result);
      return undefined;
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  private settle(result: ApiResult<unknown>): void {
    if (result.kind === 'ok') {
      this.done.emit();
      return;
    }
    this.problem.set(messageOf(result));
  }
}
