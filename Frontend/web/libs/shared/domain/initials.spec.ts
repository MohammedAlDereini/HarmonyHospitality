import { initialsOf } from './initials';

describe('initialsOf', () => {
  it('takes the first letter of the first two words', () => {
    expect(initialsOf('Layla Al-Harbi')).toBe('LA');
    expect(initialsOf('Pages Test')).toBe('PT');
    expect(initialsOf('Ahmad')).toBe('A');
  });

  it('keeps the name in its own script and ignores extra spaces', () => {
    expect(initialsOf('  ليلى   الحربي ')).toBe('لا');
    expect(initialsOf('')).toBe('');
  });
});
