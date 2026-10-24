import { DOCUMENT, Injectable, computed, effect, inject, signal } from '@angular/core';
import { Density, Look, Theme, applyLook, rememberLook, rememberedLook } from '@harmony/ui/tokens';

/** The device's storage, or null where the browser refuses it (a private window, blocked site data). */
const storageOf = (document: Document): Storage | null => {
  try {
    return document.defaultView?.localStorage ?? null;
  } catch {
    return null;
  }
};

/** Theme and density for this device: applied to <html> at once, remembered as a first-paint hint (architecture 9). */
@Injectable({ providedIn: 'root' })
export class LookStore {
  private readonly document = inject(DOCUMENT);
  private readonly storage = storageOf(this.document);
  private readonly look = signal<Look>(rememberedLook(this.storage));

  readonly theme = computed(() => this.look().theme);
  readonly density = computed(() => this.look().density);

  constructor() {
    effect(() => {
      const look = this.look();
      applyLook(this.document.documentElement, look);
      rememberLook(this.storage, look);
    });
  }

  setTheme(theme: Theme): void {
    this.look.update((look) => ({ ...look, theme }));
  }

  setDensity(density: Density): void {
    this.look.update((look) => ({ ...look, density }));
  }
}
