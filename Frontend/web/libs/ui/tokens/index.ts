/** The tokens are CSS (tokens.css, base.css), imported by each app's styles.css. Code only knows the two switches. */
export type Theme = 'light' | 'dark';
export type Density = 'comfortable' | 'standard' | 'compact';
export const themeAttribute = 'data-theme';
export const densityAttribute = 'data-density';
