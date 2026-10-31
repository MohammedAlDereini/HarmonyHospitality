/** A check's word, as the health page writes it; Unreachable is the BFF's own word for "Identity gave no report". */
export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy' | 'Unreachable';

/** One health check of Identity by name (GET /bff/health, entries[]). */
export interface HealthEntry {
  readonly name: string;
  readonly status: HealthStatus;
  /** As the service wrote it: "00:00:00.0123456". */
  readonly duration: string | null;
  readonly description: string | null;
  readonly tags: readonly string[];
}

/** This BFF host beside the services: its ring, release, floor and environment. */
export interface HostLine {
  readonly ring: string;
  readonly release: string;
  readonly minimumClientVersion: string;
  readonly environment: string;
}

/** Identity's health as the console BFF shapes it (GET /bff/health). 502 carries the same shape with status Unreachable. */
export interface HealthReport {
  readonly status: HealthStatus;
  readonly checkedAtUtc: string;
  readonly totalDuration: string | null;
  readonly entries: readonly HealthEntry[];
  readonly host: HostLine;
}

const tones: Readonly<Record<HealthStatus, 'ok' | 'warn' | 'stop'>> = {
  Healthy: 'ok',
  Degraded: 'warn',
  Unhealthy: 'stop',
  Unreachable: 'stop',
};

/** The chip tone of a status word: done, attention, critical. */
export const healthTone = (status: HealthStatus): 'ok' | 'warn' | 'stop' => tones[status];

/** Hours, minutes, seconds: the three parts of a .NET TimeSpan's text. */
const CLOCK_PARTS = 3;

/** "00:00:00.0123456" as whole milliseconds, or null when the text is not a duration. */
export const durationMs = (duration: string | null): number | null => {
  if (duration === null) return null;
  const parts = duration.split(':');
  if (parts.length !== CLOCK_PARTS) return null;
  const [hours, minutes, seconds] = parts.map(Number);
  if ([hours, minutes, seconds].some((part) => part === undefined || Number.isNaN(part)))
    return null;
  const secondsPerMinute = 60;
  const millisecondsPerSecond = 1000;
  const total =
    ((hours ?? 0) * secondsPerMinute + (minutes ?? 0)) * secondsPerMinute + (seconds ?? 0);
  return Math.round(total * millisecondsPerSecond);
};
