import { TestBed } from '@angular/core/testing';
import { LookStore } from './look-store';

describe('LookStore', () => {
  afterEach(() => {
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('data-density');
    try {
      localStorage.removeItem('harmony.look');
    } catch {
      // storage may be absent in this environment
    }
  });

  it('applies theme and density to <html> and remembers them for the next visit', () => {
    const look = TestBed.inject(LookStore);
    TestBed.tick();
    expect(document.documentElement.hasAttribute('data-theme')).toBe(false);
    look.setTheme('dark');
    look.setDensity('compact');
    TestBed.tick();
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(document.documentElement.getAttribute('data-density')).toBe('compact');
    expect(localStorage.getItem('harmony.look')).toBe('{"theme":"dark","density":"compact"}');
    look.setTheme('light');
    TestBed.tick();
    expect(document.documentElement.hasAttribute('data-theme')).toBe(false);
  });
});
