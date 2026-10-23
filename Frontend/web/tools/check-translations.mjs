// Translation check (architecture v2.3, section 4.2 tools/): every unit extracted from the source language must exist
// in every locale file with a NON-EMPTY <target>. Angular's own i18nMissingTranslation=error fires only for absent
// units; an empty <target> ships English silently (spike-proven). Node only, no packages.
import { readdirSync, readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const root = process.cwd();
const appsDir = join(root, 'apps');
let failures = 0;
let checked = 0;

const units = (xml) => {
  const map = new Map();
  const re = /<trans-unit\s+id="([^"]+)"[\s\S]*?<\/trans-unit>/g;
  for (const m of xml.matchAll(re)) {
    const body = m[0];
    const source = /<source>([\s\S]*?)<\/source>/.exec(body)?.[1] ?? '';
    const target = /<target[^>]*>([\s\S]*?)<\/target>/.exec(body)?.[1];
    map.set(m[1], { source, target });
  }
  return map;
};

for (const app of readdirSync(appsDir)) {
  const localeDir = join(appsDir, app, 'src', 'locale');
  const sourceFile = join(localeDir, 'messages.xlf');
  if (!existsSync(sourceFile)) continue;
  const source = units(readFileSync(sourceFile, 'utf8'));
  const localeFiles = readdirSync(localeDir).filter((f) => /^messages\.[a-z-]+\.xlf$/i.test(f));
  if (localeFiles.length === 0) {
    console.error(`${app}: messages.xlf exists but no messages.<locale>.xlf beside it`);
    failures++;
    continue;
  }
  for (const file of localeFiles) {
    const locale = units(readFileSync(join(localeDir, file), 'utf8'));
    for (const [id, { source: text }] of source) {
      checked++;
      const unit = locale.get(id);
      if (!unit) {
        console.error(`${app}/${file}: unit "${id}" is missing (source: "${text}")`);
        failures++;
      } else if (unit.target === undefined || unit.target.trim() === '') {
        console.error(`${app}/${file}: unit "${id}" has an empty <target> (source: "${text}")`);
        failures++;
      }
    }
    for (const id of locale.keys()) {
      if (!source.has(id)) {
        console.error(`${app}/${file}: unit "${id}" no longer exists in the source; remove it`);
        failures++;
      }
    }
  }
}

console.log(`check-translations: ${checked} units checked, ${failures} problem(s)`);
process.exit(failures === 0 ? 0 : 1);
