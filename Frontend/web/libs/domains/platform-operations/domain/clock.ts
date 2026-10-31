/** The console reads the trail in UTC, as Identity writes it: times, days and full instants in that one zone. */
const UTC = 'UTC';
const DAY_MS = 86_400_000;

const dayOf = (date: Date): number => Math.floor(date.getTime() / DAY_MS);

/** "10:42:07": the clock time of an instant, in UTC. */
export const utcTime = (instant: string, locale: string): string =>
  new Intl.DateTimeFormat(locale, {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
    timeZone: UTC,
  }).format(new Date(instant));

/** "Today", "Yesterday", or "26 Oct": which UTC day an instant fell on, seen from now. */
export const utcDay = (instant: string, locale: string, now: Date): string => {
  const gap = dayOf(now) - dayOf(new Date(instant));
  if (gap === 0) return $localize`:@@platform-operations.clock.today:Today`;
  if (gap === 1) return $localize`:@@platform-operations.clock.yesterday:Yesterday`;
  return new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'short', timeZone: UTC }).format(
    new Date(instant),
  );
};

/** "Tue 28 Oct 2026 · 10:42:07 UTC": the whole instant, for a detail. */
export const utcInstant = (instant: string, locale: string): string => {
  const date = new Date(instant);
  const day = new Intl.DateTimeFormat(locale, {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: UTC,
  }).format(date);
  return `${day} · ${utcTime(instant, locale)} UTC`;
};

/** "2026-10-28" as "28 Oct 2026". */
export const utcDate = (day: string, locale: string): string =>
  new Intl.DateTimeFormat(locale, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: UTC,
  }).format(new Date(`${day}T00:00:00Z`));

/** The instant that many days before now, as Identity reads it (ISO, UTC). */
export const daysAgo = (days: number, now: Date): string =>
  new Date(now.getTime() - days * DAY_MS).toISOString();

/** Midnight UTC of today, as an ISO instant. */
export const utcMidnight = (now: Date): string => {
  const start = new Date(now);
  start.setUTCHours(0, 0, 0, 0);
  return start.toISOString();
};
