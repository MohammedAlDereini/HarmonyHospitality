import { ApiResult } from '@harmony/platform/http';
import { ErrorKind } from './error-kind';

/** Wording per platform code, written once here in both languages (architecture 11.2: wording lives in the frontend). */
const byCode: Readonly<Record<string, string>> = {
  '000.06': $localize`:@@platform.errors.code.000-06:Still working… please wait a moment.`,
  '000.07': $localize`:@@platform.errors.code.000-07:This request was already used. Reload the page and try again.`,
  unreachable: $localize`:@@platform.errors.code.unreachable:Connection lost. Check your network and try again.`,
  // Identity, accounts
  '00036': $localize`:@@platform.errors.code.00036:That account no longer exists.`,
  '00041': $localize`:@@platform.errors.code.00041:That account is terminated; nothing more can be done to it.`,
  '00076': $localize`:@@platform.errors.code.00076:That account stands above your own rank.`,
  '00078': $localize`:@@platform.errors.code.00078:Your own account cannot be changed here. Ask another administrator.`,
  '00079': $localize`:@@platform.errors.code.00079:This is the last administrator; the tenant would be left with nobody in charge.`,
  '10303': $localize`:@@platform.errors.code.10303:Give a reason.`,
  '10304': $localize`:@@platform.errors.code.10304:The reason is too long: 500 characters at most.`,
  // Identity, a new account
  '00015': $localize`:@@platform.errors.code.00015:That role no longer exists.`,
  '00042': $localize`:@@platform.errors.code.00042:The account could not be stored. Try again.`,
  '00048': $localize`:@@platform.errors.code.00048:An account with this email already exists.`,
  '00054': $localize`:@@platform.errors.code.00054:The invitation could not be sent, so the account was not created. Try again later.`,
  '00077': $localize`:@@platform.errors.code.00077:That role stands above your own rank.`,
  '10308': $localize`:@@platform.errors.code.10308:Give a display name.`,
  '10309': $localize`:@@platform.errors.code.10309:The name is too long: 128 characters at most.`,
  '10310': $localize`:@@platform.errors.code.10310:Give an email address.`,
  '10311': $localize`:@@platform.errors.code.10311:That does not look like an email address.`,
  '10312': $localize`:@@platform.errors.code.10312:The email is too long: 256 characters at most.`,
  '10315': $localize`:@@platform.errors.code.10315:Choose a role.`,
  '10316': $localize`:@@platform.errors.code.10316:Choose a role.`,
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
