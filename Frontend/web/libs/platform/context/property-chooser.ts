import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MyProperties } from './my-properties';
import { PropertySummary, propertyName } from './property-summary';

/** The signed-in landing (architecture 10): the person's own hotels, one row each, in the prototype's menu-row look. */
@Component({
  selector: 'hr-property-chooser',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .list {
      padding: 6px var(--s4);
      max-width: 640px;
    }
  `,
  templateUrl: './property-chooser.html',
})
export class PropertyChooser {
  protected readonly properties = inject(MyProperties);

  constructor() {
    void this.properties.ready();
  }

  protected name(property: PropertySummary): string {
    return propertyName(property, $localize.locale ?? 'en');
  }

  protected tryAgain(): void {
    void this.properties.reload();
  }
}
