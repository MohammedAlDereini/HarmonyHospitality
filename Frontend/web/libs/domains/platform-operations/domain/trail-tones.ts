import { familyOf, signInOutcomeOf } from './trail';

/** The chip tones of the console (Status chip 3:115): done, attention, critical, today, long-term, neutral. */
export type TrailTone = 'ok' | 'warn' | 'stop' | 'info' | 'long' | 'neutral';

/** Actions whose tone is their own, whatever the family. */
const byAction: Readonly<Record<string, TrailTone>> = {
  'account.suspended': 'stop',
  'account.terminated': 'stop',
  'account.reinstated': 'ok',
  'account.created': 'ok',
  'account.merged': 'warn',
  'tenant.suspended': 'stop',
  'tenant.reinstated': 'ok',
  'tenant.created': 'ok',
  'tenant.administrator-invitation-failed': 'warn',
  'tenant.unsubscribed': 'warn',
  'property.created': 'ok',
  'property.rooms-above-licence': 'warn',
  'approval.requested': 'info',
  'approval.approved': 'ok',
  'approval.rejected': 'stop',
  'approval.voided': 'neutral',
  'passkey.removed-by-admin': 'warn',
  'two-step.reset-by-admin': 'warn',
  'two-step.turned-off': 'warn',
  'session.signed-out-everywhere': 'warn',
  'access.property-refused': 'stop',
  'directory.took-over': 'info',
  'directory.connection-removed': 'warn',
};

const byFamily: Readonly<Record<string, TrailTone>> = {
  'device.': 'info',
  'passkey.': 'ok',
  'password.': 'info',
  'two-step.': 'info',
  'identity.': 'info',
  'company-login.': 'info',
  'directory.': 'info',
  'config.': 'neutral',
  'property-group.': 'neutral',
  'approvals.': 'neutral',
};

const signInTones: Readonly<Record<ReturnType<typeof signInOutcomeOf>, TrailTone>> = {
  'signed-in': 'ok',
  refused: 'stop',
  held: 'warn',
  other: 'neutral',
};

/** The tone a trail row is shown in: a sign-in by its outcome, otherwise by the action or its family. */
export const trailTone = (action: string): TrailTone => {
  if (action.startsWith('sign-in.')) return signInTones[signInOutcomeOf(action)];
  return byAction[action] ?? byFamily[familyOf(action)] ?? 'neutral';
};
