import { ChangeDetectionStrategy, Component, DOCUMENT, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  SYSTEM_ACTOR,
  TrailEvent,
  TrailTone,
  trailTone,
  trailWording,
  utcInstant,
} from '@harmony/platform-operations/domain';
import { Icon } from '@harmony/ui/icons';
import { Toasts } from '@harmony/ui/overlays';

/** JSON.stringify's indentation when a row is copied for a person to read. */
const JSON_INDENT = 2;

/**
 * One trail row in full, as the Audit log's and the Sign-ins' drawers show it (Figma C8 drawer): the words and the
 * chip, every fact Identity kept, and the ways out: copy as JSON, open the customer, open the person (platform people
 * only, the console has no page for a customer's people).
 */
@Component({
  selector: 'hr-trail-event-detail',
  imports: [RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trail-event-detail.html',
  styleUrl: './trail-event-detail.css',
})
export class TrailEventDetail {
  readonly event = input.required<TrailEvent>();
  /** The platform's own tenant id; a person of that tenant has a page in this console. */
  readonly ownTenant = input<string | null>(null);
  private readonly toasts = inject(Toasts);
  private readonly document = inject(DOCUMENT);
  private readonly locale = $localize.locale ?? 'en';

  protected tone(): TrailTone {
    return trailTone(this.event().action);
  }

  protected wording(): string {
    return trailWording(this.event().action);
  }

  protected when(): string {
    return utcInstant(this.event().occurredAtUtc, this.locale);
  }

  protected isSystem(): boolean {
    return this.event().actorSubject === SYSTEM_ACTOR;
  }

  protected actorName(): string {
    if (this.isSystem()) return $localize`:@@platform-operations.trail.actor.system:system`;
    return this.event().actorDisplayName ?? this.event().actorSubject;
  }

  protected actorWhere(): string {
    if (this.isSystem())
      return $localize`:@@platform-operations.trail.actor.identity:Identity service`;
    return this.event().tenantCode ?? '';
  }

  protected ownPerson(): boolean {
    const own = this.ownTenant();
    return own !== null && this.event().tenantId === own;
  }

  protected async copy(): Promise<void> {
    const clipboard = this.document.defaultView?.navigator.clipboard;
    if (clipboard === undefined) return;
    try {
      await clipboard.writeText(JSON.stringify(this.event(), null, JSON_INDENT));
      this.toasts.show($localize`:@@platform-operations.trail.copied:Event copied as JSON.`);
    } catch {
      this.toasts.show(
        $localize`:@@platform-operations.trail.copy-failed:The browser refused the clipboard.`,
        true,
      );
    }
  }
}
