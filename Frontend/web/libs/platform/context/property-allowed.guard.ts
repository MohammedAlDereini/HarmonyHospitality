import { inject } from '@angular/core';
import { CanMatchFn } from '@angular/router';
import { SessionStore } from '@harmony/platform/session';
import { MyProperties } from './my-properties';
import { propertyParameter } from './current-property';

/** /p/:property exists for the person only among their own hotels (architecture 10). */
export const propertyAllowed: CanMatchFn = async (route, segments) => {
  const session = inject(SessionStore);
  const properties = inject(MyProperties);
  const index = (route.path ?? '').split('/').indexOf(`:${propertyParameter}`);
  const code = segments[index]?.path;
  if (code === undefined) return false;
  await session.ready();
  await properties.ready();
  return properties.list().some((property) => property.code === code);
};
