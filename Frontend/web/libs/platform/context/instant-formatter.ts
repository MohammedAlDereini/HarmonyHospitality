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
  return formatter.format(typeof instant === 'string' ? new Date(instant) : instant);
};

/** The platform console's clock: UTC, said as such wherever an instant is shown. */
@Injectable()
export class UtcInstants extends InstantFormatter {
  formatInstant(instant: string | number | Date, options?: Intl.DateTimeFormatOptions): string {
    return formatInZone(instant, 'UTC', options);
  }
}
