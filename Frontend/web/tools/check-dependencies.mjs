// Supply-chain gate (architecture 15.2, approved packages 2026-10-18): every package is one the owner approved, every
// version is exact, all @angular/* share one version, no package is named @harmony/*, and pnpm-workspace.yaml still
// carries the policy. Fails with a list of problems; prints one line when clean.
import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const manifest = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));
const policy = readFileSync(join(root, 'pnpm-workspace.yaml'), 'utf8');

/** Runtime and build packages the owner approved, by name. A new one needs the owner's yes first. */
const approved = new Set([
  '@angular/common',
  '@angular/compiler',
  '@angular/core',
  '@angular/forms',
  '@angular/localize',
  '@angular/platform-browser',
  '@angular/router',
  '@angular/cdk',
  '@angular/aria',
  'rxjs',
  'tslib',
  'web-vitals',
  '@microsoft/signalr',
  '@angular/build',
  '@angular/cli',
  '@angular/compiler-cli',
  'angular-eslint',
  'eslint',
  'typescript-eslint',
  'typescript',
  'vitest',
  'jsdom',
  'prettier',
  '@playwright/test',
  '@axe-core/playwright',
]);

/** Packages the owner rejected; their presence is a problem on its own. */
const rejected = new Set([
  'zone.js',
  'tailwindcss',
  '@angular/animations',
  '@angular/material',
  '@ngrx/store',
  '@tanstack/angular-query-experimental',
  '@sentry/angular',
  '@nx/angular',
  'dompurify',
  'eslint-plugin-boundaries',
  '@opentelemetry/sdk-trace-web',
  '@lhci/cli',
  'openapi-typescript',
]);

/** The policy lines pnpm must still read, exactly. */
const policyLines = [
  'minimumReleaseAge: 4320',
  'trustPolicy: no-downgrade',
  'strictPeerDependencies: true',
  'autoInstallPeers: false',
  'blockExoticSubdeps: true',
];

// Matches "22.2.3", "6.0.3", "2026.1.0"; rejects "^22.2.3", "~1.2", ">=1", "*", "latest", "1.2.0-beta.1".
const exact = /^\d+\.\d+\.\d+$/;

const problems = [];
const all = { ...(manifest.dependencies ?? {}), ...(manifest.devDependencies ?? {}) };
const angularVersions = new Set();

for (const [name, version] of Object.entries(all)) {
  if (name.startsWith('@harmony/')) {
    problems.push(`${name}: no package may be named @harmony/* (path aliases only)`);
  }
  if (rejected.has(name)) problems.push(`${name}: rejected by the owner`);
  else if (!approved.has(name)) {
    problems.push(`${name}: not in the approved list; ask the owner first`);
  }
  if (!exact.test(version)) problems.push(`${name}@${version}: pin the exact version`);
  if (name.startsWith('@angular/')) angularVersions.add(version);
}
if (angularVersions.size > 1) {
  problems.push(`@angular/* must share one version; found ${[...angularVersions].join(', ')}`);
}
for (const field of [
  'peerDependencies',
  'optionalDependencies',
  'overrides',
  'pnpm',
  'resolutions',
]) {
  if (manifest[field] !== undefined) {
    problems.push(`package.json "${field}" is not allowed (never override peers)`);
  }
}
for (const line of policyLines) {
  if (!policy.includes(line)) problems.push(`pnpm-workspace.yaml must say "${line}"`);
}
if (!existsSync(join(root, 'pnpm-lock.yaml'))) {
  problems.push('pnpm-lock.yaml is missing (CI installs with --frozen-lockfile)');
}

// .npmrc may name the registry; the supply-chain policy lives in pnpm-workspace.yaml only.
const npmrc = join(root, '.npmrc');
if (existsSync(npmrc)) {
  const lines = readFileSync(npmrc, 'utf8')
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line !== '' && !line.startsWith('#'));
  for (const line of lines) {
    if (!line.startsWith('registry=')) {
      problems.push(`.npmrc: "${line}" belongs in pnpm-workspace.yaml, not here`);
    }
  }
}

if (problems.length > 0) {
  console.error(`check-dependencies: ${problems.length} problem(s)`);
  for (const problem of problems) console.error(`  ${problem}`);
  process.exit(1);
}
console.log(`check-dependencies: ${Object.keys(all).length} packages checked, 0 problems`);
