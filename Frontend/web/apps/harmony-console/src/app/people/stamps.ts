import { formatInZone } from '@harmony/platform/context';

const dayFormat: Intl.DateTimeFormatOptions = { dateStyle: 'medium' };
const timeFormat: Intl.DateTimeFormatOptions = {
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
};

/** "Oct 29, 2026 09:12 UTC": the console's clock is UTC and says so; '—' for nothing. */
export const stampUtc = (instant: string | null): string =>
  instant === null
    ? '—'
    : `${formatInZone(instant, 'UTC', dayFormat)} ${formatInZone(instant, 'UTC', timeFormat)} UTC`;

/** "Sep 1, 2025", the day in UTC; '—' for nothing. */
export const dayUtc = (instant: string | null): string =>
  instant === null ? '—' : formatInZone(instant, 'UTC', dayFormat);
