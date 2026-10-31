import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OptionRows, ReferenceStore } from '@harmony/platform-admin/data-access';
import { AccessStore, IfAllowed, Permission } from '@harmony/platform/access';
import { messageOf } from '@harmony/platform/errors';
import { newIntentKey } from '@harmony/platform/http';
import { Icon } from '@harmony/ui/icons';
import { Toasts } from '@harmony/ui/overlays';
import { PageHead } from '@harmony/ui/shell';

/** A registry row as the reference store hands it over (the domain's ReferenceRow, reached through data-access). */
type ReferenceRow = ReturnType<OptionRows['rows']>[number];

/** The settings registry: Identity's ConfigSetting table, whose default is what every tenant starts from. */
const REGISTRY = 'ConfigSetting';

/** One registered setting as the page reads it. */
interface Registered {
  readonly key: string;
  readonly ownerService: string;
  readonly dataType: string;
  readonly systemDefault: string;
  readonly appliesToTypes: readonly string[];
  readonly isActive: boolean;
}

/** A service that owns settings, with how many: the rail on the left. */
interface Section {
  readonly service: string;
  readonly count: number;
}

const textOf = (row: ReferenceRow, key: string): string => {
  const value = row[key];
  if (value === null || value === undefined) return '';
  return Array.isArray(value) ? value.join(', ') : String(value);
};
const isList = (value: unknown): value is readonly string[] =>
  Array.isArray(value) && value.every((item) => typeof item === 'string');
const listOf = (row: ReferenceRow, key: string): readonly string[] => {
  const value = row[key];
  return isList(value) ? value : [];
};

/**
 * The platform's settings (platform console design, C11): the registered keys by the service that owns them, each
 * with the default every tenant starts from, changed here and saved together. A tenant, a group or a hotel may set
 * its own value; this page is the last place the resolution falls back to. The registry itself (new keys, types)
 * is a reference table.
 */
@Component({
  selector: 'hr-settings-console-page',
  imports: [RouterLink, IfAllowed, Icon, PageHead],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './settings-console-page.html',
  styleUrl: './settings-console-page.css',
})
export class SettingsConsolePage {
  private readonly reference = inject(ReferenceStore);
  private readonly access = inject(AccessStore);
  private readonly toasts = inject(Toasts);
  private readonly registry = this.reference.choices(REGISTRY);
  private readonly count = this.reference.count(REGISTRY);

  protected readonly manageTenants = Permission.ManageTenants;
  protected readonly mayManage = computed(() => this.access.can(Permission.ManageTenants));
  protected readonly saving = signal(false);
  protected readonly problem = signal<string | null>(null);
  /** The service whose settings are shown; the first one until chosen. */
  protected readonly chosenService = signal<string | null>(null);
  /** What the person typed and has not saved yet, by key. */
  private readonly draft = signal<ReadonlyMap<string, string>>(new Map());

  protected readonly settings = computed<readonly Registered[]>(() =>
    this.registry.rows().map((row) => ({
      key: textOf(row, 'key'),
      ownerService: textOf(row, 'ownerService'),
      dataType: textOf(row, 'dataType'),
      systemDefault: textOf(row, 'systemDefault'),
      appliesToTypes: listOf(row, 'appliesToTypes'),
      isActive: row['isActive'] !== false,
    })),
  );
  protected readonly loading = this.registry.loading;
  protected readonly total = computed(() => {
    const total = this.count.total();
    return typeof total === 'number' ? total : this.settings().length;
  });
  protected readonly sections = computed<readonly Section[]>(() => {
    const counts = new Map<string, number>();
    this.settings().forEach((setting) => {
      counts.set(setting.ownerService, (counts.get(setting.ownerService) ?? 0) + 1);
    });
    return [...counts.entries()]
      .map(([service, count]) => ({ service, count }))
      .sort((a, b) => a.service.localeCompare(b.service));
  });
  protected readonly current = computed<Section | null>(() => {
    const sections = this.sections();
    return (
      sections.find((section) => section.service === this.chosenService()) ?? sections[0] ?? null
    );
  });
  protected readonly shown = computed<readonly Registered[]>(() => {
    const current = this.current();
    return current === null
      ? []
      : this.settings().filter((setting) => setting.ownerService === current.service);
  });
  protected readonly changes = computed(() => {
    const settings = this.settings();
    let count = 0;
    this.draft().forEach((value, key) => {
      const setting = settings.find((candidate) => candidate.key === key);
      if (setting !== undefined && setting.systemDefault !== value) count += 1;
    });
    return count;
  });

  constructor() {
    this.registry.load();
  }

  protected choose(section: Section): void {
    this.chosenService.set(section.service);
  }

  /** The value as it stands on the page: the draft when typed, the registry's default otherwise. */
  protected valueOf(setting: Registered): string {
    return this.draft().get(setting.key) ?? setting.systemDefault;
  }

  protected isOn(setting: Registered): boolean {
    return this.valueOf(setting) === 'true';
  }

  protected changed(setting: Registered): boolean {
    const typed = this.draft().get(setting.key);
    return typed !== undefined && typed !== setting.systemDefault;
  }

  /** "int · hotel, resort" under the key: the type and the property types it applies to. */
  protected note(setting: Registered): string {
    return setting.appliesToTypes.length === 0
      ? setting.dataType
      : `${setting.dataType} · ${setting.appliesToTypes.join(', ')}`;
  }

  protected type(setting: Registered, value: string): void {
    this.draft.update((draft) => new Map(draft).set(setting.key, value));
  }

  protected toggle(setting: Registered, on: boolean): void {
    this.type(setting, on ? 'true' : 'false');
  }

  protected discard(): void {
    this.draft.set(new Map());
  }

  protected save(): void {
    void this.saveNow();
  }

  /** Every changed key is written on its own press key (UpdateConfigSettingCommand), together; the registry reads again. */
  private async saveNow(): Promise<void> {
    this.problem.set(null);
    this.saving.set(true);
    const outcomes = await Promise.all(
      this.settings()
        .filter((setting) => this.changed(setting))
        .map(async (setting) => {
          const value = this.valueOf(setting);
          const body = {
            key: setting.key,
            ownerService: setting.ownerService,
            systemDefault: value === '' ? null : value,
            appliesToTypes: setting.appliesToTypes,
            isActive: setting.isActive,
          };
          const result = await this.reference.update(REGISTRY, body, newIntentKey());
          return result.kind === 'ok' ? null : `${setting.key}: ${messageOf(result)}`;
        }),
    );
    this.saving.set(false);
    const problems = outcomes.filter((outcome): outcome is string => outcome !== null);
    if (problems.length > 0) {
      this.problem.set(problems.join(' · '));
      return;
    }
    this.draft.set(new Map());
    this.toasts.show($localize`:@@administration.console.settings.toast.saved:Defaults saved.`);
  }
}
