import { Injectable } from '@angular/core';

const defaultInstantFormat: Intl.DateTimeFormatOptions = {
  dateStyle: 'medium',
  timeStyle: 'short',
};

/**
 * Shows an instant in the zone the screen is about, in the build's language. Inside a property the context formats
 * in the property's zone; the platform console, which has no property, says UTC. The device's zone plays no part.
 */
export abstract class InstantFormatter {
  abstract formatInstant(
    instant: string | number | Date,
    options?: Intl.DateTimeFormatOptions,
  ): string;
}

// Identity's DateTime columns (CreatedDate and the like) hold UTC but reach the app without an offset; such a text is
// read as UTC, never as the device's own time. A date and a time with no zone after it: matches
// "2026-10-27T20:58:22.3407345"; rejects "2026-10-27T20:58:22Z" and "2026-10-27T20:58:22+03:00" (they say their zone).
// Tester: https://regex101.com/?regex=%5E%5Cd%7B4%7D-%5Cd%7B2%7D-%5Cd%7B2%7DT%5Cd%7B2%7D%3A%5Cd%7B2%7D(%3A%5Cd%7B2%7D(%5C.%5Cd%2B)%3F)%3F%24&testString=2026-10-27T20%3A58%3A22.3407345
const withoutZone = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?$/;

const asDate = (instant: string): Date =>
  new Date(withoutZone.test(instant) ? `${instant}Z` : instant);

/** The formatter for one named zone, or for the formatter's own default when the zone is null. */
export const formatInZone = (
  instant: string | number | Date,
  zone: string | null,
  options: Intl.DateTimeFormatOptions = defaultInstantFormat,
): string => {
  const formatter = new Intl.DateTimeFormat(
    $localize.locale ?? 'en',
    zone === null ? options : { ...options, timeZone: zone },
  );
  return formatter.format(typeof instant === 'string' ? asDate(instant) : instant);
};

/** The platform console's clock: UTC, said as such wherever an instant is shown. */
@Injectable()
export class UtcInstants extends InstantFormatter {
  formatInstant(instant: string | number | Date, options?: Intl.DateTimeFormatOptions): string {
    return formatInZone(instant, 'UTC', options);
  }
}
