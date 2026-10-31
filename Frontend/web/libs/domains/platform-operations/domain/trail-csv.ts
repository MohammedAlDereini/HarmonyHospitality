import { TrailCount, TrailEvent } from './trail';

/** The columns a CSV carries, in this order; names only, never e-mail addresses. */
const CSV_COLUMNS = [
  'occurredAtUtc',
  'action',
  'actorDisplayName',
  'actorSubject',
  'tenantCode',
  'userDisplayName',
  'detail',
  'sourceAddress',
  'device',
] as const;

/** The date part of an ISO instant: "2026-10-28". */
const ISO_DATE_LENGTH = 10;

// A cell that needs quoting: it holds a double quote, a comma or a line break. Matches 'Nile, View' and 'say "hi"';
// rejects 'plain text' (nothing to quote).
// Tester: https://regex101.com/?regex=%5B%22%2C%5Cn%5Cr%5D&flags=g&testString=Nile%2C%20View
const needsQuotes = /[",\n\r]/;

const csvCell = (value: string | null): string => {
  const text = value ?? '';
  return needsQuotes.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
};

/** The rows as CSV text with a header line and a byte-order mark, so a spreadsheet opens it as UTF-8. */
export const trailCsv = (rows: readonly TrailEvent[]): string => {
  const lines = [
    CSV_COLUMNS.join(','),
    ...rows.map((row) => CSV_COLUMNS.map((column) => csvCell(row[column])).join(',')),
  ];
  return `﻿${lines.join('\r\n')}`;
};

/** "harmony-trail-2026-10-28.csv": named after the newest row's day. */
export const trailCsvFileName = (rows: readonly TrailEvent[], prefix: string): string =>
  `${prefix}-${rows[0]?.occurredAtUtc.slice(0, ISO_DATE_LENGTH) ?? 'export'}.csv`;

/** How many rows the actions that pass the test add up to. */
export const countWhere = (
  counts: readonly TrailCount[],
  keep: (action: string) => boolean,
): number =>
  counts.filter((count) => keep(count.action)).reduce((sum, count) => sum + count.count, 0);
