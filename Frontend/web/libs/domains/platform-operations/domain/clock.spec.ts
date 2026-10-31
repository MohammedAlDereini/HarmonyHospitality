import { daysAgo, utcDate, utcDay, utcInstant, utcMidnight, utcTime } from './clock';

const NOW = new Date('2026-10-29T14:05:30Z');
const EN = 'en-GB';
const SEVEN = 7;

describe('the console clock (UTC)', () => {
  it('shows the clock time, the day relative to now, and the whole instant in UTC', () => {
    expect(utcTime('2026-10-29T10:42:07Z', EN)).toBe('10:42:07');
    expect(utcDay('2026-10-29T01:00:00Z', EN, NOW)).toBe('Today');
    expect(utcDay('2026-10-28T23:59:59Z', EN, NOW)).toBe('Yesterday');
    expect(utcDay('2026-10-26T11:02:44Z', EN, NOW)).toBe('26 Oct');
    expect(utcInstant('2026-10-28T10:42:07Z', EN)).toBe('Wed, 28 Oct 2026 · 10:42:07 UTC');
    expect(utcDate('2026-11-08', EN)).toBe('8 Nov 2026');
  });

  it('measures windows from now: days ago and midnight UTC', () => {
    expect(daysAgo(SEVEN, NOW)).toBe('2026-10-22T14:05:30.000Z');
    expect(utcMidnight(NOW)).toBe('2026-10-29T00:00:00.000Z');
  });
});
