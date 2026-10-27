import { ChangeDetectionStrategy, Component, inject, signal, viewChild } from '@angular/core';
import { FormField, form, maxLength, required, submit } from '@angular/forms/signals';
import { CompanyLoginStore } from '@harmony/administration/data-access';
import { CompanyLogin } from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { ConfirmDialog, Toasts } from '@harmony/ui/overlays';

/** A domain name has at most this many characters (FederatedAuthorityDomain.DomainMaxLength). */
const DOMAIN_MAX_LENGTH = 253;
/** The domain's own codes: they land under the field, not in the general line. */
const domainCodes: readonly string[] = ['10903', '10904', '00123', '00125'];

const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

interface DomainDraft {
  readonly domain: string;
}

/**
 * The customer's company login (stage A, the Mews way). Harmony adds Microsoft or Google for the customer; here the
 * administrator lists the company's e-mail domains, sends the consent link to the company's IT, and switches it on.
 * From then on everyone on those domains types their e-mail and signs in with the company; the person page keeps
 * exceptions on a Harmony password. One intent key per press; every refusal worded once.
 */
@Component({
  selector: 'hr-company-login-page',
  imports: [FormField, IfAllowed, ConfirmDialog],
  providers: [CompanyLoginStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './company-login-page.html',
  styleUrl: './company-login-page.css',
})
export class CompanyLoginPage {
  protected readonly store = inject(CompanyLoginStore);
  private readonly toasts = inject(Toasts);
  private readonly onDialog = viewChild.required<ConfirmDialog>('onDialog');

  protected readonly manageUsers = Permission.ManageUsers;
  protected readonly busy = signal(false);
  protected readonly problem = signal<string | null>(null);
  /** The login whose add-domain field holds the draft (one form serves each login's section in turn). */
  protected readonly adding = signal<string | null>(null);

  private readonly model = signal<DomainDraft>({ domain: '' });
  protected readonly form = form(this.model, (path) => {
    required(path.domain, { message: wording('10903') });
    maxLength(path.domain, DOMAIN_MAX_LENGTH, { message: wording('10904') });
  });
  protected readonly domainProblem = signal<string | null>(null);

  protected startAdding(login: CompanyLogin): void {
    this.problem.set(null);
    this.domainProblem.set(null);
    this.model.set({ domain: '' });
    this.adding.set(login.id);
  }

  protected async addDomain(event: Event, login: CompanyLogin): Promise<void> {
    event.preventDefault();
    await submit(this.form, async () => {
      this.domainProblem.set(null);
      const result = await this.run(
        this.store.addDomain(login, this.model().domain.trim(), newIntentKey()),
      );
      if (result.kind === 'ok') {
        this.adding.set(null);
        this.toasts.show(
          $localize`:@@administration.company-login.toast.domain-added:Domain added.`,
        );
        return;
      }

      const codes = 'codes' in result ? result.codes : [];
      if (codes.some((code) => domainCodes.includes(code))) {
        this.domainProblem.set(messageOf(result));
      } else {
        this.problem.set(messageOf(result));
      }
    });
  }

  protected async removeDomain(login: CompanyLogin, domain: string): Promise<void> {
    const result = await this.run(this.store.removeDomain(login, domain, newIntentKey()));
    if (result.kind === 'ok')
      this.toasts.show(
        $localize`:@@administration.company-login.toast.domain-removed:Domain removed.`,
      );
    else this.problem.set(messageOf(result));
  }

  /** On is asked first: from the next sign-in, everyone on the domains signs in with the company. */
  protected async switchOn(login: CompanyLogin): Promise<void> {
    if (!(await this.onDialog().ask())) return;
    await this.switchTo(login, true);
  }

  protected switchOff(login: CompanyLogin): void {
    void this.switchTo(login, false);
  }

  private async switchTo(login: CompanyLogin, on: boolean): Promise<void> {
    const result = await this.run(this.store.switch(login, on, newIntentKey()));
    if (result.kind === 'ok') {
      this.toasts.show(
        on
          ? $localize`:@@administration.company-login.toast.on:Company sign-in is on.`
          : $localize`:@@administration.company-login.toast.off:Company sign-in is off.`,
      );
    } else {
      this.problem.set(messageOf(result));
    }
  }

  /** One press: nothing else until it settles. */
  private async run(call: Promise<ApiResult<unknown>>): Promise<ApiResult<unknown>> {
    this.problem.set(null);
    this.busy.set(true);
    const result = await call;
    this.busy.set(false);
    return result;
  }
}
