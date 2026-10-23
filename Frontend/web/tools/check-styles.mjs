// Style check (architecture v2.3, F16 and 15): colour literals only inside libs/ui/tokens; logical CSS properties
// only (no left/right: direction is markup, never CSS); a link that opens a new window carries rel="noopener noreferrer".
// Looks at every .css file and at the inline `styles` and `template` of components. Node only, no packages.
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const root = process.cwd();
const roots = ['apps', 'libs'].map((d) => join(root, d));
const tokensDir = join(root, 'libs', 'ui', 'tokens');
let failures = 0;
let files = 0;

function* walk(dir) {
  let names;
  try {
    names = readdirSync(dir);
  } catch {
    return;
  }
  for (const name of names) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) yield* walk(p);
    else if (/\.(css|ts|html)$/.test(p) && !p.endsWith('.spec.ts')) yield p;
  }
}

const colour = /(#[0-9a-f]{3,8}\b|\brgba?\(|\bhsla?\()/i;
const physical =
  /(^|[\s;{])(margin-left|margin-right|padding-left|padding-right|border-left|border-right|left|right|float)\s*:|text-align\s*:\s*(left|right)\b/i;
const newWindow = /<a\b[^>]*\btarget\s*=/gi;

function checkCss(file, css, allowColour) {
  for (const [i, line] of css.split('\n').entries()) {
    const text = line.replace(/\/\*.*?\*\//g, '');
    if (!allowColour && colour.test(text))
      report(file, i + 1, 'colour literal: use a token from libs/ui/tokens');
    if (physical.test(text))
      report(
        file,
        i + 1,
        'physical direction: use a logical property (inline-start/end, block-start/end)',
      );
  }
}
function checkTemplate(file, html) {
  for (const m of html.matchAll(newWindow)) {
    const tag = html.slice(m.index, html.indexOf('>', m.index) + 1);
    if (
      !/rel\s*=\s*["'][^"']*noopener[^"']*noreferrer|rel\s*=\s*["'][^"']*noreferrer[^"']*noopener/i.test(
        tag,
      )
    ) {
      report(
        file,
        html.slice(0, m.index).split('\n').length,
        'a link with a target needs rel="noopener noreferrer"',
      );
    }
  }
}
function report(file, line, rule) {
  console.error(`${relative(root, file)}:${line}  ${rule}`);
  failures++;
}

for (const base of roots) {
  for (const file of walk(base)) {
    files++;
    const text = readFileSync(file, 'utf8');
    const inTokens = file.startsWith(tokensDir + sep);
    if (file.endsWith('.css')) {
      checkCss(file, text, inTokens);
    } else if (file.endsWith('.html')) {
      checkTemplate(file, text);
    } else {
      // Components: inline styles and templates in the .ts file.
      for (const m of text.matchAll(/styles\s*:\s*(\[\s*)?`([\s\S]*?)`/g))
        checkCss(file, m[2], false);
      for (const m of text.matchAll(/template\s*:\s*`([\s\S]*?)`/g)) checkTemplate(file, m[1]);
    }
  }
}

console.log(`check-styles: ${files} files checked, ${failures} problem(s)`);
process.exit(failures === 0 ? 0 : 1);
