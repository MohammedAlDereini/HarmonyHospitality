import { Injectable, inject, makeEnvironmentProviders } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

/** "Users · Harmony": the route's localized title first, the product last; "Harmony" alone where a route has none. */
@Injectable()
export class HarmonyTitles extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly product = $localize`:@@ui.shell.product:Harmony`;

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const page = this.buildTitle(snapshot);
    this.title.setTitle(page === undefined ? this.product : `${page} · ${this.product}`);
  }
}

export const provideHarmonyTitles = () =>
  makeEnvironmentProviders([{ provide: TitleStrategy, useClass: HarmonyTitles }]);
