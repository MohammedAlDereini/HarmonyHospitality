// Measures the built apps against the approved budgets (architecture 13: initial raw <= 500 kB error / 400 kB warning;
// any lazy script <= 250 kB). Reads dist/<app>/browser after `ng build`; the initial set is what each locale's
// index.html loads, the lazy set is every other script. Fails on an error-level breach; warnings only print.
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const KILOBYTE = 1024;
const INITIAL_WARNING = 400 * KILOBYTE;
const INITIAL_ERROR = 500 * KILOBYTE;
const LAZY_ERROR = 250 * KILOBYTE;
const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const apps =
  process.argv.slice(2).length > 0 ? process.argv.slice(2) : ['harmony-web', 'harmony-console'];
const locales = ['en', 'ar'];
const kb = (bytes) => `${(bytes / KILOBYTE).toFixed(1)} kB`;

// Matches src="polyfills-ABC123.js", href="styles-XYZ.css"; rejects inline scripts and data: URLs.
const assetRef = /(?:src|href)="(?!data:)([^"]+\.(?:js|css))"/g;

let errors = 0;
let warnings = 0;
for (const app of apps) {
  const browser = join(root, 'dist', app, 'browser');
  if (!existsSync(browser)) {
    console.error(`size-report: ${browser} is missing; run ng build ${app} first`);
    errors += 1;
    continue;
  }
  // Each locale folder holds its own scripts; whatever its index.html does not load is lazy.
  const lazy = [];
  for (const locale of locales) {
    const page = join(browser, locale, 'index.html');
    if (!existsSync(page)) {
      console.error(`size-report: ${app}/${locale}: index.html is missing`);
      errors += 1;
      continue;
    }
    const html = readFileSync(page, 'utf8');
    const refs = [
      ...new Set([...html.matchAll(assetRef)].map((m) => m[1].replace(`/${locale}/`, ''))),
    ];
    const initial = refs.map((ref) => {
      const file = join(browser, locale, ref);
      return { name: ref, bytes: existsSync(file) ? statSync(file).size : 0 };
    });
    const total = initial.reduce((sum, file) => sum + file.bytes, 0);
    const folder = join(browser, locale);
    readdirSync(folder)
      .filter((name) => name.endsWith('.js') && !refs.includes(name))
      .forEach((name) =>
        lazy.push({ name: `${locale}/${name}`, bytes: statSync(join(folder, name)).size }),
      );
    const level = total > INITIAL_ERROR ? 'ERROR' : total > INITIAL_WARNING ? 'WARN' : 'ok';
    if (level === 'ERROR') errors += 1;
    if (level === 'WARN') warnings += 1;
    console.log(
      `${app}/${locale}: initial ${kb(total)} (${initial.map((f) => `${f.name} ${kb(f.bytes)}`).join(', ')}) ${level}`,
    );
  }
  for (const chunk of lazy) {
    if (chunk.bytes > LAZY_ERROR) {
      errors += 1;
      console.log(`${app}: lazy ${chunk.name} ${kb(chunk.bytes)} ERROR (limit ${kb(LAZY_ERROR)})`);
    }
  }
  const largest = lazy.reduce((top, chunk) => (chunk.bytes > top.bytes ? chunk : top), {
    name: '-',
    bytes: 0,
  });
  console.log(`${app}: ${lazy.length} lazy scripts, largest ${largest.name} ${kb(largest.bytes)}`);
}
console.log(`size-report: ${errors} error(s), ${warnings} warning(s)`);
if (errors > 0) process.exit(1);
