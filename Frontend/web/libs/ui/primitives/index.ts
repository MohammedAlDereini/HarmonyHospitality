/**
 * The prototype's controls, menus and rows as classes (primitives.css, imported by each app's styles.css): .btn,
 * .pbtn, .seg, .tlink, .field, .sec-h, .scroll, .empty, .pop and its parts, .srow and its parts, .ng-h, .ni.
 * Components wrapping them arrive with the first forms (ui/forms) and overlays (ui/overlays).
 */

/** The operational tone of a mark: red intervenes, amber is emerging, green is verified, blue is active. */
export type Tone = 'stop' | 'warn' | 'ok' | 'info';

/** Rows and marks say their tone in a data attribute; text says it too, never colour alone. */
export const toneAttribute = 'data-s';
