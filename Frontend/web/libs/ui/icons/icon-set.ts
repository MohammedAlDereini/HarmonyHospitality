/**
 * The prototype's icon line (home(8), "One line of icons, drawn on a 16 grid at a 1.5 stroke like the bar's own"),
 * copied shape by shape. Attribute values stay strings: they are drawing, not arithmetic.
 */
export type Shape =
  | { readonly kind: 'path'; readonly d: string }
  | {
      readonly kind: 'circle';
      readonly cx: string;
      readonly cy: string;
      readonly r: string;
      readonly filled?: true;
    }
  | {
      readonly kind: 'rect';
      readonly x: string;
      readonly y: string;
      readonly width: string;
      readonly height: string;
      readonly rx: string;
    };

export interface IconDrawing {
  readonly viewBox: string;
  readonly strokeWidth: string;
  readonly shapes: readonly Shape[];
}

const path = (d: string): Shape => ({ kind: 'path', d });
const circle = (cx: string, cy: string, r: string): Shape => ({ kind: 'circle', cx, cy, r });
const dot = (cx: string, cy: string, r: string): Shape => ({
  kind: 'circle',
  cx,
  cy,
  r,
  filled: true,
});
const rect = (x: string, y: string, width: string, height: string, rx: string): Shape => ({
  kind: 'rect',
  x,
  y,
  width,
  height,
  rx,
});

const grid16 = '0 0 16 16';
const stroke = '1.5';
const line = (...shapes: Shape[]): IconDrawing => ({
  viewBox: grid16,
  strokeWidth: stroke,
  shapes,
});

export const icons = {
  home: line(
    path('M2.5 7.25 8 2.75l5.5 4.5V13a.75.75 0 0 1-.75.75H3.25A.75.75 0 0 1 2.5 13z'),
    path('M6.5 13.75V10h3v3.75'),
  ),
  desk: line(
    path('M2 12.5h12'),
    path('M3.25 12.5a4.75 4.75 0 0 1 9.5 0'),
    path('M8 7.75V5.5'),
    path('M6.75 5.5h2.5'),
  ),
  rooms: line(
    path('M1.75 4v9'),
    path('M1.75 10.25h12.5V13'),
    path('M14.25 10.25V8.5a1.75 1.75 0 0 0-1.75-1.75H7.25v3.5'),
    circle('4.5', '8.5', '1.25'),
  ),
  guest: line(circle('8', '5.25', '2.5'), path('M3 13.75c.4-2.6 2.4-4.25 5-4.25s4.6 1.65 5 4.25')),
  folio: line(
    path('M3.75 1.75h8.5v12.5l-2.1-1.25-2.15 1.25-2.15-1.25-2.1 1.25z'),
    path('M6 5.25h4M6 8h4'),
  ),
  tape: line(
    rect('2', '2.75', '7', '2.5', '1.25'),
    rect('5.5', '6.75', '8.5', '2.5', '1.25'),
    rect('2', '10.75', '9', '2.5', '1.25'),
  ),
  panel: line(
    rect('2', '2.75', '12', '10.5', '1.5'),
    path('M6 2.75v10.5'),
    path('M8.75 6.5 10.25 8l-1.5 1.5'),
  ),
  caret: line(path('M4 6l4 4 4-4')),
  more: line(dot('3.5', '8', '1.1'), dot('8', '8', '1.1'), dot('12.5', '8', '1.1')),
  plus: line(path('M8 3.5v9M3.5 8h9')),
  check: line(path('M3.5 8.5 6.5 11.5 12.5 4.75')),
  star: line(
    path('M8 2.2l1.75 3.55 3.9.57-2.82 2.75.66 3.9L8 11.14l-3.49 1.83.66-3.9L2.35 6.32l3.9-.57z'),
  ),
  go: line(path('M3 8h9.5M9 4.5 12.5 8 9 11.5')),
  /** The bar's own three: the menu, the search and the close, as the prototype drew them inline. */
  menu: { viewBox: '0 0 18 18', strokeWidth: '1.6', shapes: [path('M3 5h12M3 9h12M3 13h12')] },
  search: {
    viewBox: grid16,
    strokeWidth: '1.7',
    shapes: [circle('7', '7', '4.5'), path('M10.5 10.5L14 14')],
  },
  close: { viewBox: grid16, strokeWidth: '1.7', shapes: [path('M4 4l8 8M12 4l-8 8')] },
} as const satisfies Record<string, IconDrawing>;

export type IconName = keyof typeof icons;
