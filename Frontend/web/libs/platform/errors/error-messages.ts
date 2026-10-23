import { ApiResult } from '@harmony/platform/http';
import { ErrorKind } from './error-kind';

/** Wording per platform code, written once here in both languages (architecture 11.2: wording lives in the frontend). */
const byCode: Readonly<Record<string, string>> = {
  '000.06': $localize`:@@platform.errors.code.000-06:Still working… please wait a moment.`,
  '000.07': $localize`:@@platform.errors.code.000-07:This request was already used. Reload the page and try again.`,
  unreachable: $localize`:@@platform.errors.code.unreachable:Connection lost. Check your network and try again.`,
};

/** Wording per kind, used when no code has its own (the "Person sees" column of architecture 14). */
const byKind: Readonly<Record<ErrorKind, string>> = {
  refused: $localize`:@@platform.errors.kind.refused:Harmony did not allow this action.`,
  invalid: $localize`:@@platform.errors.kind.invalid:Some fields need attention.`,
  unauthenticated: $localize`:@@platform.errors.kind.unauthenticated:Sign in again to continue.`,
  forbidden: $localize`:@@platform.errors.kind.forbidden:Your role cannot do this.`,
  busy: $localize`:@@platform.errors.kind.busy:Still working… please wait a moment.`,
  conflict: $localize`:@@platform.errors.kind.conflict:Changed by someone else. Reload to see the latest.`,
  unavailable: $localize`:@@platform.errors.kind.unavailable:Harmony is not responding. Try again in a moment.`,
  unexpected: $localize`:@@platform.errors.kind.unexpected:Something went wrong.`,
};

/** The wording for one code, or undefined while the catalogue has none (check-error-codes will list those, D25). */
export const errorMessage = (code: string): string | undefined => byCode[code];

/** The wording for a kind. */
export const kindMessage = (kind: ErrorKind): string => byKind[kind];

type Failed = Exclude<ApiResult<unknown>, { kind: 'ok' }>;

/** One sentence for a failed outcome: the first code with wording, else the kind's. */
export const messageOf = (result: Failed): string =>
  ('codes' in result
    ? result.codes.map(errorMessage).find((text) => text !== undefined)
    : undefined) ?? kindMessage(result.kind);
