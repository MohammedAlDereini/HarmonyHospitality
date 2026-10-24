// Keeps the frontend's error wording honest against the framework's code catalogue (architecture 11.2, D25): every
// code worded in libs/platform/errors/error-messages.ts must exist in Harmony.Core/Errors/*.cs, so a typo or a
// retired code never ships silently. Codes the framework has but the frontend does not word are listed with
// --verbose; they fall back to the kind's wording and are not a failure (not every code reaches a screen).
// The framework lives beside the repository; HARMONY_FRAMEWORK_ERRORS points elsewhere when it does not.
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const catalogue = resolve(
  process.env.HARMONY_FRAMEWORK_ERRORS ??
    join(root, '..', '..', '..', 'Harmony.Framework', 'Harmony.Core', 'Errors'),
);
const wordingFile = join(root, 'libs', 'platform', 'errors', 'error-messages.ts');
const verbose = process.argv.includes('--verbose');

/** Frontend-only codes: the HTTP layer names them when no answer arrived at all. */
const frontendOnly = new Set(['unreachable']);

if (!existsSync(catalogue)) {
  console.error(`check-error-codes: catalogue folder not found: ${catalogue}`);
  process.exit(2);
}

// Matches `public const string HigherRank = "00076";` and captures the name and the code; rejects other constants.
const constant = /public const string (\w+) = "([0-9.]+)";/g;
const known = new Map();
for (const name of readdirSync(catalogue).filter((file) => file.endsWith('.cs'))) {
  const text = readFileSync(join(catalogue, name), 'utf8');
  for (const match of text.matchAll(constant)) {
    const [, member, code] = match;
    if (known.has(code)) {
      console.error(
        `check-error-codes: ${code} is declared twice in the framework (${known.get(code)} and ${name}:${member})`,
      );
      process.exit(1);
    }
    known.set(code, `${name}:${member}`);
  }
}

// Matches the keys of byCode: `'00076': $localize` or `'000.06': $localize`; rejects other quoted strings.
const worded = /^\s*'([0-9.]+|[a-z]+)': \$localize/gm;
const wording = readFileSync(wordingFile, 'utf8');
const used = [...wording.matchAll(worded)].map((match) => match[1]);

const unknown = used.filter((code) => !frontendOnly.has(code) && !known.has(code));
const unworded = [...known.keys()].filter((code) => !used.includes(code));

if (unknown.length > 0) {
  console.error(
    `check-error-codes: ${unknown.length} worded code(s) the framework does not declare: ${unknown.join(', ')}`,
  );
  process.exit(1);
}
console.log(
  `check-error-codes: ${used.length} worded codes all declared; ${known.size} declared in the framework, ${unworded.length} without wording (fall back to the kind)`,
);
if (verbose) {
  for (const code of unworded) console.log(`  ${code}  ${known.get(code)}`);
}
