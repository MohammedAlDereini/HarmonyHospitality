/**
 * A cell that must be quoted: it holds a quote, a comma, a semicolon or a line break. Matches 'Ritz, Amman' and
 * 'say "hi"'; rejects 'RITZ'. Tester: https://regex101.com/r/Xo4jKc/1
 */
const needsQuotes = /[",;\r\n]/;

/**
 * A typed text that a spreadsheet would run as a formula: it starts with =, +, -, @, a tab or a carriage return
 * (OWASP CSV injection). Matches '=HYPERLINK("x")' and '+cmd'; rejects 'Amman' and '12'.
 * Tester: https://regex101.com/?regex=%5E%5B%3D%2B%5C-%40%5Ct%5Cr%5D&flags=gm&testString=%3DHYPERLINK(%22x%22)%0A%2Bcmd%0AAmman%0A12
 */
const formulaStart = /^[=+\-@\t\r]/;

/**
 * One CSV cell: quoted when it has to be, its quotes doubled (RFC 4180). A typed text that a spreadsheet would run
 * as a formula is kept as text with a leading apostrophe; numbers pass as they are, negative ones included.
 */
export const csvCell = (value: string | number | null | undefined): string => {
  if (value === null || value === undefined) return '';
  const text = typeof value === 'string' && formulaStart.test(value) ? `'${value}` : String(value);
  return needsQuotes.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
};

/** One CSV line from its cells. */
export const csvLine = (cells: readonly (string | number | null | undefined)[]): string =>
  cells.map(csvCell).join(',');

/** A whole CSV file: the header, then one line per row, CRLF between lines (RFC 4180). */
export const csvFile = (
  header: readonly string[],
  rows: readonly (readonly (string | number | null | undefined)[])[],
): string => [csvLine(header), ...rows.map(csvLine)].join('\r\n');
