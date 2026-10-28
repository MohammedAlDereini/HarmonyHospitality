import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { TenantDirectoryStore } from '@harmony/platform-admin/data-access';
import { IfAllowed, Permission } from '@harmony/platform/access';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { ConfirmDialog, Toasts } from '@harmony/ui/overlays';

/**
 * A customer's company directory in the console (stage E): on or off, its people and groups, and with ManageTenants the
 * removal, asked first. The customer's administrator opens it and maps its groups in the web app.
 */
@Component({
  selector: 'hr-directory-panel',
  imports: [IfAllowed, ConfirmDialog],
  providers: [TenantDirectoryStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './directory-panel.html',
  styleUrl: './directory-panel.css',
})
export class DirectoryPanel {
  readonly tenantId = input.required<string>();

  protected readonly store = inject(TenantDirectoryStore);
  private readonly toasts = inject(Toasts);
  private readonly removeDialog = viewChild.required<ConfirmDialog>('removeDialog');
  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly busy = signal(false);
  protected readonly problem = signal<string | null>(null);

  constructor() {
    effect(() => {
      const id = this.tenantId();
      untracked(() => {
        this.store.ofTenant(id);
      });
    });
  }

  protected async remove(): Promise<void> {
    if (!(await this.removeDialog().ask())) return;
    this.problem.set(null);
    this.busy.set(true);
    const result = await this.store.remove(newIntentKey());
    this.busy.set(false);
    if (result.kind === 'ok')
      this.toasts.show(
        $localize`:@@platform-admin.directory.toast.removed:The company directory is removed; its people stay with the customer.`,
      );
    else this.problem.set(messageOf(result));
  }
}
