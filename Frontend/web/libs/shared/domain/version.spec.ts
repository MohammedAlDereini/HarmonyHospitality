import { compareVersions } from './version';

describe('compareVersions', () => {
  it('orders dotted versions part by part; a missing part is zero; garbage is zero', () => {
    expect(compareVersions('1.2.0', '1.2.0')).toBe(0);
    expect(compareVersions('1.2', '1.2.0')).toBe(0);
    expect(compareVersions('1.2.1', '1.10.0')).toBeLessThan(0);
    expect(compareVersions('2.0.0', '1.99.99')).toBeGreaterThan(0);
    expect(compareVersions('0.0.0', '')).toBe(0);
    expect(compareVersions('1.x.0', '1.0.0')).toBe(0);
  });
});
