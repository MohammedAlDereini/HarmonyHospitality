import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { PermissionEntry } from '@harmony/administration/domain';
import { permissionLabel } from './wording';

/** The permission catalogue as a checklist; the parent owns the chosen set. */
@Component({
  selector: 'hr-permission-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './permission-picker.html',
  styleUrl: './permission-picker.css',
})
export class PermissionPicker {
  readonly entries = input.required<readonly PermissionEntry[]>();
  readonly selected = input.required<ReadonlySet<string>>();
  /** The id of the permission whose box was ticked or cleared. */
  readonly toggled = output<string>();

  protected label(entry: PermissionEntry): string {
    return permissionLabel(entry.permissionValue, entry.name);
  }
}
