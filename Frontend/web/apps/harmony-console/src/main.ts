/// <reference types="@angular/localize" />
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

// The locale is baked into each build; direction follows it, in markup, before the first paint.
document.documentElement.dir = ($localize.locale ?? 'en').startsWith('ar') ? 'rtl' : 'ltr';

bootstrapApplication(App, appConfig).catch((err) => console.error(err));