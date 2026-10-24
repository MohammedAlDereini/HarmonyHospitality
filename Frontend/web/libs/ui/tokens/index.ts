/** The tokens are CSS (tokens.css, base.css), imported by each app's styles.css. Code only knows the two switches. */
export type { Density, Look, Theme } from './look';
export {
  applyLook,
  defaultLook,
  densityAttribute,
  rememberLook,
  rememberedLook,
  themeAttribute,
} from './look';
