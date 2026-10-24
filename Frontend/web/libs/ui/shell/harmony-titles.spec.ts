import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideHarmonyTitles } from './harmony-titles';

@Component({ selector: 'hr-blank', changeDetection: ChangeDetectionStrategy.OnPush, template: '' })
class Blank {}

describe('HarmonyTitles', () => {
  it('names the page first and the product last; the product alone without a route title', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHarmonyTitles(),
        provideRouter([
          { path: 'users', component: Blank, title: 'Users' },
          { path: 'plain', component: Blank },
        ]),
      ],
    });
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/users');
    expect(document.title).toBe('Users · Harmony');
    await router.navigateByUrl('/plain');
    expect(document.title).toBe('Harmony');
  });
});
