import { familyOf, signInOutcomeOf } from './trail';
import { countWhere, trailCsv, trailCsvFileName } from './trail-csv';
import { trailTone } from './trail-tones';
import { familyWording, hasTrailWording, trailWording } from './trail-wording';
import { TrailEvent } from './trail';

const row: TrailEvent = {
  id: 'e1',
  tenantId: 't1',
  tenantCode: 'RITZ',
  tenantNameEn: 'Ritz Amman Hotels LLC',
  tenantNameAr: 'فنادق ريتز عمان',
  userAccountId: 'u1',
  userDisplayName: 'Amal Haddad',
  action: 'tenant.suspended',
  actorSubject: 'u9',
  actorDisplayName: 'Platform Admin',
  sourceAddress: '::1',
  device: 'Chrome on Windows, says "hi"',
  occurredAtUtc: '2026-10-28T10:42:07Z',
  detail: 'RITZ',
};

const TEN = 10;
const SEVEN = 7;
const THREE = 3;

describe('the trail words, families, tones and counts', () => {
  it('knows a sign-in that got in, one refused and one held; anything else is other', () => {
    expect(signInOutcomeOf('sign-in.passkey')).toBe('signed-in');
    expect(signInOutcomeOf('sign-in.wrong-password')).toBe('refused');
    expect(signInOutcomeOf('sign-in.new-device-held')).toBe('held');
    expect(signInOutcomeOf('tenant.created')).toBe('other');
  });

  it('cuts the family at the first dot', () => {
    expect(familyOf('property-group.member-added')).toBe('property-group.');
    expect(familyOf('odd')).toBe('odd');
  });

  it('has words for the actions this build knows and falls back to the code for the rest', () => {
    expect(trailWording('tenant.suspended')).toBe('Tenant suspended');
    expect(hasTrailWording('tenant.suspended')).toBe(true);
    expect(trailWording('billing.invoice-overdue')).toBe('billing.invoice-overdue');
    expect(hasTrailWording('billing.invoice-overdue')).toBe(false);
    expect(familyWording('sign-in.')).toBe('Sign-ins');
  });

  it('tones: a refused sign-in is critical, a suspension critical, a device trusted informative, an unknown action neutral', () => {
    expect(trailTone('sign-in.locked-out')).toBe('stop');
    expect(trailTone('sign-in.passkey')).toBe('ok');
    expect(trailTone('tenant.suspended')).toBe('stop');
    expect(trailTone('device.trusted')).toBe('info');
    expect(trailTone('something.new')).toBe('neutral');
  });

  it('adds up the counts of the actions that pass a test', () => {
    const counts = [
      { action: 'sign-in.passkey', count: SEVEN },
      { action: 'sign-in.wrong-code', count: THREE },
      { action: 'tenant.created', count: 1 },
    ];
    expect(countWhere(counts, (a) => signInOutcomeOf(a) === 'signed-in')).toBe(SEVEN);
    expect(countWhere(counts, (a) => a.startsWith('sign-in.'))).toBe(TEN);
    expect(countWhere([], () => true)).toBe(0);
  });

  it('writes a CSV with a header, quotes cells that need it, and names the file after the newest row', () => {
    const csv = trailCsv([row]);
    const lines = csv.split('\r\n');
    expect(lines[0]).toBe(
      '﻿occurredAtUtc,action,actorDisplayName,actorSubject,tenantCode,userDisplayName,detail,sourceAddress,device',
    );
    expect(lines[1]).toBe(
      '2026-10-28T10:42:07Z,tenant.suspended,Platform Admin,u9,RITZ,Amal Haddad,RITZ,::1,"Chrome on Windows, says ""hi"""',
    );
    expect(trailCsvFileName([row], 'harmony-trail')).toBe('harmony-trail-2026-10-28.csv');
    expect(trailCsvFileName([], 'harmony-trail')).toBe('harmony-trail-export.csv');
  });
});
