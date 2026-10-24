import { Injectable } from '@angular/core';

/**
 * Tells property-scoped stores when the property changed (architecture 7.7): route injectors outlive a navigation,
 * so a store never dies on a switch; it clears itself here instead.
 */
@Injectable({ providedIn: 'root' })
export class PropertyScope {
  private readonly listeners = new Set<() => void>();

  /** Called on every property switch; returns the stop function. */
  onReset(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  reset(): void {
    this.listeners.forEach((listener) => {
      listener();
    });
  }
}
