import { SignInOutcome } from './trail';

/** How a sign-in row proved the person, read from its action name: the Sign-ins page's Method column and filter. */
export type SignInMethod = 'password' | 'two-step' | 'passkey' | 'email-link' | 'company' | 'other';

const methodsByAction: Readonly<Record<string, SignInMethod>> = {
  'sign-in.password-only': 'password',
  'sign-in.wrong-password': 'password',
  'sign-in.locked-out': 'password',
  'sign-in.password-grant': 'password',
  'sign-in.two-step': 'two-step',
  'sign-in.after-two-step-setup': 'two-step',
  'sign-in.wrong-code': 'two-step',
  'sign-in.password-grant-two-step': 'two-step',
  'sign-in.passkey': 'passkey',
  'sign-in.passkey-refused': 'passkey',
  'sign-in.email-link': 'email-link',
  'sign-in.company': 'company',
  'sign-in.company-refused': 'company',
};

export const signInMethodOf = (action: string): SignInMethod => methodsByAction[action] ?? 'other';

/** Every action of a method, for the filter: the server narrows by these names. */
export const actionsOfMethod = (method: SignInMethod): readonly string[] =>
  Object.entries(methodsByAction)
    .filter(([, candidate]) => candidate === method)
    .map(([action]) => action);

const methodWords: Readonly<Record<SignInMethod, string>> = {
  password: $localize`:@@platform-operations.sign-ins.method.password:Password`,
  'two-step': $localize`:@@platform-operations.sign-ins.method.two-step:Password + two-step`,
  passkey: $localize`:@@platform-operations.sign-ins.method.passkey:Passkey`,
  'email-link': $localize`:@@platform-operations.sign-ins.method.email-link:E-mail link`,
  company: $localize`:@@platform-operations.sign-ins.method.company:Company account`,
  other: $localize`:@@platform-operations.sign-ins.method.other:Other`,
};

export const signInMethodWording = (method: SignInMethod): string => methodWords[method];

const outcomeWords: Readonly<Record<SignInOutcome, string>> = {
  'signed-in': $localize`:@@platform-operations.sign-ins.outcome.signed-in:Signed in`,
  refused: $localize`:@@platform-operations.sign-ins.outcome.refused:Refused`,
  held: $localize`:@@platform-operations.sign-ins.outcome.held:Waiting for "Trust device"`,
  other: $localize`:@@platform-operations.sign-ins.outcome.other:Other`,
};

export const signInOutcomeWording = (outcome: SignInOutcome): string => outcomeWords[outcome];

/** The methods the filter offers, in the order shown. */
export const SIGN_IN_METHODS: readonly SignInMethod[] = [
  'password',
  'two-step',
  'passkey',
  'email-link',
  'company',
];

/** The outcomes the filter offers. */
export const SIGN_IN_OUTCOMES: readonly SignInOutcome[] = ['signed-in', 'refused', 'held'];
