// Writes the release into libs/shared/domain/version.ts before a build, so the running page knows its own build
// (architecture D38: the BFF's minimumClientVersion is compared against it). Usage:
//   node tools/stamp-version.mjs 2026.10.24.1          writes the version
//   node tools/stamp-version.mjs 2026.10.24.1 --check  fails when the file says another version
// The release name comes from the pipeline (the BFF serves the same string); a working copy keeps 0.0.0.
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const file = join(here, '..', 'libs', 'shared', 'domain', 'version.ts');
const [version, mode] = process.argv.slice(2);
// Matches "2026.10.24.1", "1.2.0", "0.0.0"; rejects "v1", "1.2.0-beta", "" (dotted numbers only, 1 to 4 parts).
const versionShape = /^\d+(?:\.\d+){0,3}$/;

if (version === undefined || !versionShape.test(version)) {
  console.error('stamp-version: give a dotted numeric version, e.g. 2026.10.24.1');
  process.exit(2);
}

const line = /^export const APP_VERSION = '([^']*)';$/m;
const source = readFileSync(file, 'utf8');
const match = line.exec(source);
if (match === null) {
  console.error(`stamp-version: ${file} has no APP_VERSION line`);
  process.exit(2);
}

if (mode === '--check') {
  if (match[1] !== version) {
    console.error(`stamp-version: ${file} says ${match[1]}, expected ${version}`);
    process.exit(1);
  }
  console.log(`stamp-version: ${version} confirmed`);
  process.exit(0);
}

writeFileSync(file, source.replace(line, `export const APP_VERSION = '${version}';`));
console.log(`stamp-version: ${match[1]} -> ${version}`);
