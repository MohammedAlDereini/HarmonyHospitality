/**
 * What the person reads for each code Identity can answer with. The codes are the contract (framework
 * BusinessErrorCodes / ModelValidationErrorCodes); the words live here, in the app, where the language is chosen.
 */
const messages: Record<string, string> = {
  // the call never reached Identity: it is down, or this web origin is not in its AllowedOrigins
  identity_unreachable: 'Identity cannot be reached from this page. Is it running, and is this address allowed in its settings?',

  // Duende token endpoint
  invalid_credentials: 'Wrong e-mail or password.',
  account_not_active: 'This account cannot sign in. Contact an administrator.',
  locked_out: 'Too many wrong attempts. The account is locked for a few minutes.',
  tenant_required: 'This e-mail exists in more than one company. Contact an administrator.',
  mfa_required: 'A code from your authenticator app is needed.',
  mfa_token_invalid: 'The sign-in step timed out. Start again.',
  otp_invalid: 'The code did not verify.',
  otp_or_recovery_code_required: 'Enter the code from the app, or one recovery code.',
  invalid_grant: 'Sign-in refused.',

  // business refusals
  '00036': 'The account was not found.',
  '00041': 'This account is terminated.',
  '00042': 'The identity store refused the change.',
  '00044': 'The current password is wrong.',
  '00045': 'The password must be at least 12 characters with an upper-case letter, a lower-case letter and a digit.',
  '00046': 'The new password must be different from the current one.',
  '00047': 'Too many wrong attempts. The account is locked for a few minutes.',
  '00053': 'This link is no longer valid. Ask for a new one.',
  '00054': 'The invitation e-mail could not be sent.',

  // model validation
  '10305': 'The current password is required.',
  '10306': 'The new password is required.',
  '10307': 'The new password must be at least 12 characters.',
  '10310': 'The e-mail is required.',
  '10311': 'Enter a valid e-mail address.',
  '10312': 'The e-mail is too long.',
  '10317': 'The code is required.',
  '10318': 'The code is 6 digits.',
  '10319': 'The link is incomplete.',
}

export function messageFor(code: string): string {
  return messages[code] ?? `Something went wrong (${code}).`
}

export function messagesFor(codes: string[]): string {
  return codes.length ? codes.map(messageFor).join(' ') : 'Something went wrong.'
}
