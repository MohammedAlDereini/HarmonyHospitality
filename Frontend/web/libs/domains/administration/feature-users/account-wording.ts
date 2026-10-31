import { AccountState, UserAccount } from '@harmony/administration/domain';
import { Tone } from '@harmony/ui/primitives';

const stateWording: Readonly<Record<AccountState, string>> = {
  [AccountState.active]: $localize`:@@administration.users.state.active:Active`,
  [AccountState.suspended]: $localize`:@@administration.users.state.suspended:Suspended`,
  [AccountState.terminated]: $localize`:@@administration.users.state.terminated:Terminated`,
};

/** Text says the state; the tone only underlines it (status never by colour alone). */
const stateTone: Readonly<Record<AccountState, Tone | null>> = {
  [AccountState.active]: null,
  [AccountState.suspended]: 'warn',
  [AccountState.terminated]: 'stop',
};

export const stateLabel = (user: Pick<UserAccount, 'state'>): string => stateWording[user.state];

export const toneOf = (user: Pick<UserAccount, 'state'>): Tone | null => stateTone[user.state];

/** Exempt beats the authenticator: a person on the list is never asked for a code. */
export const twoStepLabel = (
  user: Pick<UserAccount, 'twoFactorEnabled' | 'twoStepExemptReason'>,
): string => {
  if (user.twoStepExemptReason !== null)
    return $localize`:@@administration.users.two-step.exempt:Exempt`;
  return user.twoFactorEnabled
    ? $localize`:@@administration.users.two-step.on:On`
    : $localize`:@@administration.users.two-step.off:Not set`;
};
