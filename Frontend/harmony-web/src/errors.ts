/**
 * What the person reads for each code Identity can answer with. The codes are the contract (framework
 * BusinessErrorCodes / ModelValidationErrorCodes); the words live here, in the app, where the language is chosen.
 */
const messages: Record<string, string> = {
  // the call never reached Identity: it is down, or this web origin is not in its AllowedOrigins
  identity_unreachable: 'Identity cannot be reached from this page. Is it running, and is this address allowed in its settings?',
  session_expired: 'Your session has ended. Sign in again.',
  forbidden: 'Your role does not allow this.',
  '000.4': 'Your role does not allow this.',

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

  // accounts
  '00036': 'The account was not found.',
  '00041': 'This account is terminated.',
  '00042': 'The identity store refused the change.',
  '00044': 'The current password is wrong.',
  '00045': 'The password must be at least 12 characters with an upper-case letter, a lower-case letter and a digit.',
  '00046': 'The new password must be different from the current one.',
  '00047': 'Too many wrong attempts. The account is locked for a few minutes.',
  '00049': 'Two-step sign-in is already on.',
  '00050': 'Two-step sign-in is not on.',
  '00051': 'The code did not verify. Check the time on your phone and try the next code.',
  '00052': 'Scan the key first, then enter the code it gives.',
  '00053': 'This link is no longer valid. Ask for a new one.',
  '00054': 'The invitation e-mail could not be sent.',

  // reference data (one set for the ten tables)
  '00055': 'This row no longer exists.',
  '00056': 'A row with this code already exists.',
  '00057': 'A city with this English name already exists in that country.',
  '00058': 'That country is not in the Countries list.',
  '00059': 'That subdivision is not in the list, or belongs to another country.',
  '00060': 'That time zone is not in the Time zones list.',
  '00061': 'That currency is not in the Currencies list.',
  '00062': 'That currency code is not one Harmony can price in (not in the money registry).',
  '00063': 'That time zone id is not one this server recognises.',
  '00064': 'This is a system row: it can be renamed, but its exclusivity is fixed.',
  '00065': 'Another country already uses this alpha-3 code.',

  // model validation: accounts
  '10305': 'The current password is required.',
  '10306': 'The new password is required.',
  '10307': 'The new password must be at least 12 characters.',
  '10310': 'The e-mail is required.',
  '10311': 'Enter a valid e-mail address.',
  '10312': 'The e-mail is too long.',
  '10317': 'The code is required.',
  '10318': 'The code is 6 digits.',
  '10319': 'The link is incomplete.',

  // model validation: paging
  '10001': 'The page number must be 1 or more.',
  '10002': 'The page size must be between 1 and 100.',
  '10003': 'The list cannot be sorted by that column.',

  // model validation: reference data
  '10401': 'The code is required.',
  '10402': 'The code is not in the right format for this table.',
  '10403': 'Choose a kind: legal, tax, accounting or accommodation.',
  '10404': 'The country is required.',
  '10405': 'The country code is 2 letters.',
  '10406': 'The English name is required.',
  '10407': 'The English name is at most 128 characters.',
  '10408': 'The Arabic name is required.',
  '10409': 'The Arabic name is at most 128 characters.',
  '10410': 'The owner service is required.',
  '10411': 'The owner service is at most 64 characters.',
  '10412': 'The alpha-3 code is required.',
  '10413': 'The alpha-3 code is 3 letters.',
  '10414': 'The dial code is required.',
  '10415': 'The dial code is a plus sign and 1 to 4 digits, like +962.',
  '10416': 'The default currency is required.',
  '10417': 'The default currency is a 3-letter code.',
  '10418': 'The subdivision code is the country, a hyphen, then 1 to 3 letters or digits, like JO-AM.',
  '10419': 'The subdivision kind is required.',
  '10420': 'The subdivision kind is at most 32 characters.',
  '10421': 'The time zone id is required.',
  '10422': 'The time zone id looks wrong; it reads like Asia/Amman.',
  '10423': 'Latitude is between -90 and 90.',
  '10424': 'Longitude is between -180 and 180.',
  '10425': 'The native name is required.',
  '10426': 'The native name is at most 128 characters.',
  '10427': 'The display order is zero or more.',
  '10428': 'The row id is missing.',
}

export function messageFor(code: string): string {
  return messages[code] ?? `Something went wrong (${code}).`
}

export function messagesFor(codes: string[]): string {
  return codes.length ? [...new Set(codes.map(messageFor))].join(' ') : 'Something went wrong.'
}
