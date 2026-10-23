/**
 * One key per user intent (a press), reused for that intent's retries within the server's 10-minute replay window
 * (architecture 11.5). A new press is a new intent and a new key.
 */
export interface IntentKey {
  readonly value: string;
  readonly createdAt: number;
}

const REPLAY_WINDOW_MINUTES = 10;
const SECONDS_PER_MINUTE = 60;
const MILLISECONDS_PER_SECOND = 1000;

export const intentWindowMs = REPLAY_WINDOW_MINUTES * SECONDS_PER_MINUTE * MILLISECONDS_PER_SECOND;
export const intentHeader = 'Idempotency-Key';

export const newIntentKey = (now = Date.now()): IntentKey => ({
  value: crypto.randomUUID(),
  createdAt: now,
});

export const isFresh = (key: IntentKey, now = Date.now()): boolean =>
  now - key.createdAt < intentWindowMs;
