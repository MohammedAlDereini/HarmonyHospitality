import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormField, form, maxLength, required, submit } from '@angular/forms/signals';
import { CompanyLoginStore, UsersStore } from '@harmony/administration/data-access';
import { CompanyLogin, CompanyProvider } from '@harmony/administration/domain';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { errorMessage, kindMessage, messageOf } from '@harmony/platform/errors';
import { ApiResult, newIntentKey } from '@harmony/platform/http';
import { IdentityConfirmation, SessionStore } from '@harmony/platform/session';
import { Icon } from '@harmony/ui/icons';
import { ConfirmDialog, Toasts } from '@harmony/ui/overlays';
import { PeopleHead } from './people-head';

/** A domain name has at most this many characters (FederatedAuthorityDomain.DomainMaxLength). */
const DOMAIN_MAX_LENGTH = 253;
/** The domain's own codes: they land under the field, not in the general line. */
const domainCodes: readonly string[] = ['10903', '10904', '00123', '00125'];
/** The providers Harmony knows; a card each, added or not. */
const providers: readonly CompanyProvider[] = ['Microsoft', 'Google'];

const wording = (code: string): string => errorMessage(code) ?? kindMessage('invalid');

interface DomainDraft {
  readonly domain: string;
}

/**
 * The platform's own company login (platform console design, C21, a tab of People): one card per provider with the
 * consent step, the approval page, the people on it and its e-mail domains; a provider Harmony has not added yet says
 * so and points at the door (our own tenant's page). Switching on is asked first. One intent key per press.
 */
@Component({
  selector: 'hr-company-login-console-page',
  imports: [RouterLink, FormField, IfAllowed, Icon, ConfirmDialog, PeopleHead],
  providers: [CompanyLoginStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './company-login-console-page.html',
  styleUrl: './company-login-console-page.css',
})
export class CompanyLoginConsolePage {
  protected readonly store = inject(CompanyLoginStore);
  private readonly users = inject(UsersStore);
  private readonly session = inject(SessionStore);
  private readonly toasts = inject(Toasts);
  private readonly confirmation = inject(IdentityConfirmation);
  private readonly onDialog = viewChild.required<ConfirmDialog>('onDialog');

  protected readonly manageUsers = Permission.ManageUsers;
  protected readonly busy = signal(false);
  protected readonly problem = signal<string | null>(null);
  /** The login whose add-domain field holds the draft (one form serves each card in turn). */
  protected readonly adding = signal<string | null>(null);
  protected readonly domainProblem = signal<string | null>(null);
  private readonly model = signal<DomainDraft>({ domain: '' });
  protected readonly form = form(this.model, (path) => {
    required(path.domain, { message: wording('10903') });
    maxLength(path.domain, DOMAIN_MAX_LENGTH, { message: wording('10904') });
  });
  /** The providers Harmony has not added for us yet. */
  protected readonly missing = computed<readonly CompanyProvider[]>(() => {
    const added = new Set(this.store.rows().map((login) => login.provider));
    return providers.filter((provider) => !added.has(provider));
  });
  /** Our own tenant's page, where a provider is added (a platform action); null until the session is known. */
  protected readonly ownTenant = computed(() => this.session.user()?.tenant ?? null);

  /** How many of our people have an e-mail on one of the login's domains. */
  protected peopleOn(login: CompanyLogin): string {
    const everyone = this.users.everyone();
    const covered = everyone.filter((person) =>
      login.domains.some((domain) => this.onDomain(person.email, domain)),
    ).length;
    return $localize`:@@administration.console.company-login.people-of:${String(covered)}:covered: of ${String(everyone.length)}:total:`;
  }

  protected peopleOnDomain(domain: string): number {
    return this.users.everyone().filter((person) => this.onDomain(person.email, domain)).length;
  }

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
    const result = await this.run(
      this.confirmation.run((intent) => this.store.switch(login, on, intent)),
    );
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

  private onDomain(email: string | null, domain: string): boolean {
    return email?.toLowerCase().endsWith(`@${domain.toLowerCase()}`) ?? false;
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
