import {
  EnvironmentInjector,
  Provider,
  effect,
  inject,
  runInInjectionContext,
  signal,
} from '@angular/core';
import { AttentionItem, CONSOLE_ATTENTION } from '@harmony/ui/console-shell';

/**
 * The bell's list, without the store's weight in the first load: the shell gets an empty signal at once, and the
 * attention store (and the operations domain it reads) arrives as its own chunk a moment later, after which the
 * signal follows the store. The home imports the same store directly, so both see one set of reads.
 */
export const provideConsoleAttention = (): Provider => ({
  provide: CONSOLE_ATTENTION,
  useFactory: () => {
    const injector = inject(EnvironmentInjector);
    const items = signal<readonly AttentionItem[]>([]);
    const follow = async (): Promise<void> => {
      const { AttentionStore } = await import('./attention-store');
      const store = injector.get(AttentionStore);
      runInInjectionContext(injector, () => {
        effect(() => {
          items.set(store.items());
        });
      });
    };
    void follow();
    return items.asReadonly();
  },
});
