import { DOCUMENT, InjectionToken, inject } from '@angular/core';

/** Non-secret runtime settings the BFF writes into index.html on every request (architecture 4, C-30, D33). */
export interface RuntimeConfig {
  /** The BFF's environment name, as the BFF names it. */
  readonly environment: string;
  /** The release the person is running. */
  readonly release: string;
  /** The oldest release still allowed to run; older pages must reload (platform/app-update, later). */
  readonly minimumClientVersion: string;
}

/**
 * The element the BFF adds inside <head>: <script id="harmony-config" type="application/json">{…}</script>.
 * A JSON data block is never run, so it needs no nonce and cannot carry code.
 */
export const runtimeConfigElementId = 'harmony-config';

/** Until the BFF adds the element (D33, typed by the owner), a page runs as a local build. */
const localDefaults: RuntimeConfig = {
  environment: 'local',
  release: 'local',
  minimumClientVersion: '0',
};

const parse = (text: string): unknown => {
  try {
    return JSON.parse(text);
  } catch (error: unknown) {
    throw new Error(`${runtimeConfigElementId}: not valid JSON`, { cause: error });
  }
};

/** Absent element: the local defaults. Present but wrong: the page must not start (fail fast, never guess). */
export function readRuntimeConfig(document: Document): RuntimeConfig {
  const element = document.getElementById(runtimeConfigElementId);
  if (element === null) return localDefaults;
  const parsed = parse(element.textContent);
  if (typeof parsed !== 'object' || parsed === null) {
    throw new Error(`${runtimeConfigElementId}: not an object`);
  }
  const record = parsed as Record<string, unknown>;
  // Only the three named fields cross into the app; anything else in the element is ignored, never merged.
  const read = (field: keyof RuntimeConfig): string => {
    const value = record[field];
    if (typeof value !== 'string' || value.trim() === '') {
      throw new Error(`${runtimeConfigElementId}: ${field} is missing or empty`);
    }
    return value;
  };
  return {
    environment: read('environment'),
    release: read('release'),
    minimumClientVersion: read('minimumClientVersion'),
  };
}

/** The runtime settings, read once from the page; tests provide their own. */
export const RUNTIME_CONFIG = new InjectionToken<RuntimeConfig>('RUNTIME_CONFIG', {
  providedIn: 'root',
  factory: () => readRuntimeConfig(inject(DOCUMENT)),
});
