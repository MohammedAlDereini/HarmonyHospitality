import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { SettingsStore } from '@harmony/administration/data-access';
import { EffectiveSetting } from '@harmony/administration/domain';
import { AccessStore, Permission } from '@harmony/platform/access';
import { Drawer, Toasts } from '@harmony/ui/overlays';
import { PageHead } from '@harmony/ui/shell';
import { SettingDetail } from './setting-detail';
import { outcomeLabel, sourceLabel } from './wording';

/**
 * The hotel's settings (design 5.1): every key the services registered, what it is at the hotel in the URL and where
 * that value comes from (this hotel, one of its groups, the whole customer, or the default). A row opens the drawer
 * with the trace; with ManageSettings the drawer also sets or clears the value at one of the trace's places.
 */
@Component({
  selector: 'hr-settings-page',
  imports: [Drawer, SettingDetail, PageHead],
  providers: [SettingsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.css',
})
export class SettingsPage {
  protected readonly store = inject(SettingsStore);
  private readonly access = inject(AccessStore);
  private readonly toasts = inject(Toasts);
  private readonly drawer = viewChild.required<Drawer>('settingDrawer');

  protected readonly mayManage = computed(() => this.access.can(Permission.ManageSettings));
  /** The key open in the drawer; its row is read again from the store after a write. */
  protected readonly openKey = signal<string | null>(null);
  protected readonly open = computed<EffectiveSetting | null>(() => {
    const key = this.openKey();
    return key === null ? null : (this.store.settings().find((s) => s.key === key) ?? null);
  });
  protected readonly count = computed(() => this.store.settings().length);

  protected source(setting: EffectiveSetting): string {
    return sourceLabel(setting);
  }

  protected outcome(setting: EffectiveSetting): string | null {
    return outcomeLabel(setting);
  }

  protected openSetting(setting: EffectiveSetting): void {
    this.openKey.set(setting.key);
    this.drawer().open();
  }

  protected onSaved(): void {
    this.toasts.show($localize`:@@administration.settings.toast.saved:Setting saved.`);
    this.drawer().close();
  }

  protected onCleared(): void {
    this.toasts.show(
      $localize`:@@administration.settings.toast.cleared:Value cleared; the next place down applies.`,
    );
    this.drawer().close();
  }
}
