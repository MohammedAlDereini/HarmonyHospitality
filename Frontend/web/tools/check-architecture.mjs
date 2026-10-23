// Architecture fitness check (architecture v2.3, 5.1), Node + the TypeScript compiler only, no package.
// 1) a library is entered only through its index.ts (@harmony/<group>/<name>); 2) the layer rules; 3) cross-domain
// only into another domain's data-access; 4) no cycles; 5) shared and domain stay framework-free; 6) the app walls.
// Exit code 1 on any violation; the report lists file, import and rule.
import ts from 'typescript';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const root = process.argv[2] ?? '.';
const libsDir = join(root, 'libs');
const appsDir = join(root, 'apps');

// Library = folder with an index.ts under libs/<group>/<name> or libs/domains/<domain>/<type>; an app = apps/<name>.
function libraryOf(file) {
  const fromApps = relative(appsDir, file).split(sep);
  if (fromApps[0] !== '..' && !fromApps[0].includes(':')) {
    return { id: `apps/${fromApps[0]}`, group: 'app', domain: null, type: 'app', app: fromApps[0] };
  }
  const parts = relative(libsDir, file).split(sep);
  if (parts[0] === 'domains') {
    return {
      id: `domains/${parts[1]}/${parts[2]}`,
      group: 'domain',
      domain: parts[1],
      type: parts[2].replace(/^feature-.*/, 'feature'),
    };
  }
  return { id: `${parts[0]}/${parts[1]}`, group: parts[0], domain: null, type: parts[0] }; // platform | ui | shared
}
function aliasToLibrary(spec) {
  // @harmony/<domain>/<type>  or  @harmony/<group>/<name>
  const m = /^@harmony\/([^/]+)\/([^/]+)(\/.*)?$/.exec(spec);
  if (!m) return null;
  const deep = Boolean(m[3]);
  if (['platform', 'ui', 'shared'].includes(m[1])) {
    return { id: `${m[1]}/${m[2]}`, group: m[1], domain: null, type: m[1], deep };
  }
  return {
    id: `domains/${m[1]}/${m[2]}`,
    group: 'domain',
    domain: m[1],
    type: m[2].replace(/^feature-.*/, 'feature'),
    deep,
  };
}

// Who may depend on whom (by type). Same-domain rules; cross-domain is handled separately.
const allowed = {
  app: ['feature', 'data-access', 'domain', 'ui', 'platform', 'shared'],
  feature: ['data-access', 'domain', 'ui', 'platform', 'shared'],
  'data-access': ['domain', 'platform', 'shared'],
  domain: ['shared'],
  'ui-domain': ['domain', 'ui', 'shared'], // a domain's own ui library
  ui: ['ui', 'shared'],
  platform: ['platform', 'shared'],
  shared: ['shared'],
};
const typeKey = (lib) => (lib.group === 'domain' && lib.type === 'ui' ? 'ui-domain' : lib.type);

// The app walls (architecture 3 and 4.1): platform people never act inside a hotel; hotel staff never see the platform.
const consoleDomains = new Set(['platform-admin', 'identity']);
const appWall = (from, to) =>
  (from.app === 'harmony-web' &&
    to.domain === 'platform-admin' &&
    'harmony-web never imports platform-admin') ||
  (from.app === 'harmony-console' &&
    to.domain &&
    !consoleDomains.has(to.domain) &&
    'harmony-console never imports a hotel domain') ||
  null;

function* tsFiles(dir) {
  if (!existsSync(dir)) return;
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) yield* tsFiles(p);
    else if (p.endsWith('.ts') && !p.endsWith('.spec.ts')) yield p;
  }
}
function importsOf(file) {
  const source = ts.createSourceFile(
    file,
    readFileSync(file, 'utf8'),
    ts.ScriptTarget.Latest,
    true,
  );
  const found = [];
  const visit = (node) => {
    if (
      (ts.isImportDeclaration(node) || ts.isExportDeclaration(node)) &&
      node.moduleSpecifier &&
      ts.isStringLiteral(node.moduleSpecifier)
    ) {
      found.push(node.moduleSpecifier.text);
    }
    if (
      ts.isCallExpression(node) &&
      node.expression.kind === ts.SyntaxKind.ImportKeyword &&
      node.arguments[0] &&
      ts.isStringLiteral(node.arguments[0])
    ) {
      found.push(node.arguments[0].text);
    }
    ts.forEachChild(node, visit);
  };
  visit(source);
  return found;
}

const violations = [];
const graph = new Map();
for (const file of [...tsFiles(libsDir), ...tsFiles(appsDir)]) {
  const from = libraryOf(file);
  if (!graph.has(from.id)) graph.set(from.id, new Set());
  for (const spec of importsOf(file)) {
    if (spec.startsWith('.')) {
      // A relative import must stay inside its own library or app.
      const target = libraryOf(join(file, '..', spec));
      if (target.id !== from.id) {
        violations.push([file, spec, 'relative import leaves its library; use the @harmony alias']);
      }
      continue;
    }
    const to = aliasToLibrary(spec);
    if (!to) {
      // npm package: governed by the dependency register; the pure layers stay framework-free.
      if (['domain', 'shared'].includes(typeKey(from)) && spec.startsWith('@angular/')) {
        violations.push([
          file,
          spec,
          `${typeKey(from)} must stay framework-free (no @angular imports)`,
        ]);
      }
      continue;
    }
    if (to.id === from.id) {
      violations.push([file, spec, 'a library imports itself through its alias']);
      continue;
    }
    graph.get(from.id).add(to.id);
    if (to.deep)
      violations.push([file, spec, 'deep import: only the library entry (index.ts) is public']);
    const wall = appWall(from, to);
    if (wall) {
      violations.push([file, spec, wall]);
      continue;
    }
    const crossDomain = from.domain && to.domain && from.domain !== to.domain;
    if (crossDomain) {
      if (!(from.type === 'feature' && to.type === 'data-access')) {
        violations.push([
          file,
          spec,
          'cross-domain: only a feature may use another domain, and only its data-access',
        ]);
      }
      continue;
    }
    const toType = to.type === 'ui' && to.group === 'domain' ? 'ui' : to.type;
    if (!allowed[typeKey(from)]?.includes(toType)) {
      violations.push([file, spec, `${typeKey(from)} may not depend on ${to.type}`]);
    }
  }
}

// Cycles between libraries (Tarjan's strongly connected components).
let index = 0;
const stack = [];
const meta = new Map();
const cycles = [];
function strong(v) {
  meta.set(v, { index, low: index, on: true });
  index++;
  stack.push(v);
  for (const w of graph.get(v) ?? []) {
    if (!meta.has(w)) {
      strong(w);
      meta.get(v).low = Math.min(meta.get(v).low, meta.get(w).low);
    } else if (meta.get(w).on) {
      meta.get(v).low = Math.min(meta.get(v).low, meta.get(w).index);
    }
  }
  if (meta.get(v).low === meta.get(v).index) {
    const scc = [];
    let w;
    do {
      w = stack.pop();
      meta.get(w).on = false;
      scc.push(w);
    } while (w !== v);
    if (scc.length > 1) cycles.push(scc);
  }
}
for (const v of graph.keys()) if (!meta.has(v)) strong(v);

for (const [file, spec, rule] of violations)
  console.log(`BOUNDARY  ${relative(root, file)}  '${spec}'  ${rule}`);
for (const c of cycles) console.log(`CYCLE     ${c.join(' -> ')}`);
console.log(
  `${graph.size} libraries and apps, ${violations.length} boundary violations, ${cycles.length} cycles`,
);
process.exit(violations.length || cycles.length ? 1 : 0);
