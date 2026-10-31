import { durationMs, healthTone } from './health';
import { actionsOfMethod, signInMethodOf, signInMethodWording } from './sign-in-wording';

const TWELVE_MS = 12;
const ONE_MINUTE_MS = 61_500;

describe('health words and sign-in methods', () => {
  it('tones a status: healthy is done, degraded attention, the rest critical', () => {
    expect(healthTone('Healthy')).toBe('ok');
    expect(healthTone('Degraded')).toBe('warn');
    expect(healthTone('Unhealthy')).toBe('stop');
    expect(healthTone('Unreachable')).toBe('stop');
  });

  it('reads a .NET duration as milliseconds, or nothing when it is not one', () => {
    expect(durationMs('00:00:00.0123456')).toBe(TWELVE_MS);
    expect(durationMs('00:01:01.5')).toBe(ONE_MINUTE_MS);
    expect(durationMs(null)).toBeNull();
    expect(durationMs('fast')).toBeNull();
  });

  it('names how a sign-in proved the person and lists every action of a method', () => {
    expect(signInMethodOf('sign-in.passkey')).toBe('passkey');
    expect(signInMethodOf('sign-in.wrong-code')).toBe('two-step');
    expect(signInMethodOf('sign-in.new-device-held')).toBe('other');
    expect(signInMethodWording('company')).toBe('Company account');
    expect(actionsOfMethod('passkey')).toEqual(['sign-in.passkey', 'sign-in.passkey-refused']);
  });
});
