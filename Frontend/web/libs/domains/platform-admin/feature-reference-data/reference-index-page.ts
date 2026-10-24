import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { tables } from './tables';

/** The reference tables, one link each. */
@Component({
  selector: 'hr-reference-index-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sec-h">
      <h1 i18n="@@platform-admin.reference.title">Reference data</h1>
    </div>
    <div class="rows" role="list">
      @for (table of tables; track table.key) {
        <div class="srow irow" role="listitem">
          <div class="s-who">
            <div class="s-nm">
              <b
                ><a class="nm-link" [routerLink]="[table.key]">{{ table.title }}</a></b
              >
              <span dir="ltr">/api/{{ table.controller }}</span>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .irow {
      grid-template-columns: minmax(0, 1fr);
    }
    .nm-link {
      color: inherit;
      text-decoration: none;
    }
    .nm-link:hover {
      text-decoration: underline;
    }
  `,
})
export class ReferenceIndexPage {
  protected readonly tables = tables;
}
