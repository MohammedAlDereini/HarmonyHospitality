import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { FormField, form, maxLength, required, submit } from '@angular/forms/signals';
import { CompanyLoginsStore } from '@harmony/platform-admin/data-access';
import { COMPANY_PROVIDERS, CompanyProvider } from '@harmony/platform-admin/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { Toasts } from '@harmony/ui/overlays';

/** A domain name has at most this many characters (FederatedAuthorityDomain.DomainMaxLength). */
const DOMAIN_MAX_LENGTH = 253;

const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

interface Draft {
  readonly provider: CompanyProvider;
  readonly domain: string;
}

/**
 * A customer's company logins in the console (stage A, "the company chose Microsoft, we set it"): what the customer
 * has, and with ManageTenants a Signal Form that adds Microsoft or Google with one of the company's domains. The
 * customer's administrator lists more domains and switches it on in the web app.
 */
@Component({
  selector: 'hr-company-login-panel',
  imports: [FormField, IfAllowed],
  providers: [CompanyLoginsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './company-login-panel.html',
  styleUrl: './company-login-panel.css',
})
export class CompanyLoginPanel {
  readonly tenantId = input.required<string>();

  protected readonly store = inject(CompanyLoginsStore);
  private readonly toasts = inject(Toasts);
  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly providers = COMPANY_PROVIDERS;
  protected readonly adding = signal(false);
  protected readonly busy = signal(false);
  protected readonly problem = signal<string | null>(null);

  private readonly model = signal<Draft>({ provider: 'Microsoft', domain: '' });
  protected readonly form = form(this.model, (path) => {
    required(path.domain, { message: wording('10903') });
    maxLength(path.domain, DOMAIN_MAX_LENGTH, { message: wording('10904') });
  });

  constructor() {
    effect(() => {
      const id = this.tenantId();
      untracked(() => {
        this.store.ofTenant(id);
      });
    });
  }

  protected start(): void {
    this.problem.set(null);
    this.model.set({ provider: 'Microsoft', domain: '' });
    this.adding.set(true);
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.form, async () => {
      this.problem.set(null);
      this.busy.set(true);
      const draft = this.model();
      const result = await this.store.add(draft.provider, draft.domain.trim(), newIntentKey());
      this.busy.set(false);
      if (result.kind === 'ok') {
        this.adding.set(false);
        this.toasts.show(
          $localize`:@@platform-admin.company-login.toast.added:Company login added. The customer's administrator switches it on.`,
        );
      } else {
        this.problem.set(messageOf(result));
      }
    });
  }
}
