export type Theme = 'light' | 'dark';
export type Density = 'comfortable' | 'standard' | 'compact';
export const themeAttribute = 'data-theme';
export const densityAttribute = 'data-density';

/** The two switches a person owns on this device (architecture 9: hints only, never personal data). */
export interface Look {
  readonly theme: Theme;
  readonly density: Density;
}

export const defaultLook: Look = { theme: 'light', density: 'standard' };

const storageKey = 'harmony.look';
const themes: readonly Theme[] = ['light', 'dark'];
const densities: readonly Density[] = ['comfortable', 'standard', 'compact'];

const isTheme = (value: unknown): value is Theme => themes.includes(value as Theme);
const isDensity = (value: unknown): value is Density => densities.includes(value as Density);

/** What this device remembered, or the default; storage may be absent or refuse, which is fine. */
export function rememberedLook(storage: Storage | null): Look {
  try {
    const raw = storage?.getItem(storageKey);
    if (raw === null || raw === undefined) return defaultLook;
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== 'object' || parsed === null) return defaultLook;
    const { theme, density } = parsed as { theme?: unknown; density?: unknown };
    return {
      theme: isTheme(theme) ? theme : defaultLook.theme,
      density: isDensity(density) ? density : defaultLook.density,
    };
  } catch {
    return defaultLook;
  }
}

/** A first-paint hint for the next visit on this device; a refusal (private window, full disk) changes nothing. */
export function rememberLook(storage: Storage | null, look: Look): void {
  try {
    storage?.setItem(storageKey, JSON.stringify(look));
  } catch {
    // The hint is a convenience; the page works without it.
  }
}

/** Applies the switches the way the tokens read them: attributes on <html>, absent for the defaults. */
export function applyLook(root: HTMLElement, look: Look): void {
  if (look.theme === defaultLook.theme) root.removeAttribute(themeAttribute);
  else root.setAttribute(themeAttribute, look.theme);
  if (look.density === defaultLook.density) root.removeAttribute(densityAttribute);
  else root.setAttribute(densityAttribute, look.density);
}
