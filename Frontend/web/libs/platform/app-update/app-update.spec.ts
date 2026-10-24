import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RUNTIME_CONFIG } from '@harmony/platform/config';
import { FlagsStore } from '@harmony/platform/flags';
import { AppUpdate } from './app-update';

describe('AppUpdate', () => {
  const release = signal<string | null>(null);
  const minimum = signal<string | null>(null);
  let update: AppUpdate;

  const configure = (minimumClientVersion: string): void => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: RUNTIME_CONFIG,
          useValue: { environment: 'test', release: '1.2.0', minimumClientVersion },
        },
        { provide: FlagsStore, useValue: { release, minimumClientVersion: minimum } },
      ],
    });
    update = TestBed.inject(AppUpdate);
  };

  beforeEach(() => {
    release.set(null);
    minimum.set(null);
  });

  it('no floor: never stale; a floor above the build (0.0.0) from the page or from the door: stale', () => {
    configure('');
    expect(update.stale()).toBe(false);
    minimum.set('0');
    expect(update.stale()).toBe(false);
    minimum.set('0.0.1');
    expect(update.stale()).toBe(true);
    TestBed.resetTestingModule();
    configure('1.0.0');
    expect(update.stale()).toBe(true);
  });

  it('newer: the door serves a release other than the one the page booted with', () => {
    configure('');
    expect(update.newer()).toBe(false);
    release.set('1.2.0');
    expect(update.newer()).toBe(false);
    release.set('1.3.0');
    expect(update.newer()).toBe(true);
  });
});
