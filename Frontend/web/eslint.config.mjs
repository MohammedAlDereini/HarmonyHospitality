// Lint rules (architecture v2.3, 7.16 and 15): the Angular and TypeScript rule sets, strict and type-aware, plus the
// house rules that must fail a build, never be remembered. The architecture checker (tools/check-architecture.mjs)
// adds the cross-library rules ESLint cannot see.
import { defineConfig } from 'eslint/config';
import angular from 'angular-eslint';
import tseslint from 'typescript-eslint';

export default defineConfig(
  { ignores: ['dist/**', 'node_modules/**', '.angular/**', 'obj/**', 'bin/**'] },
  {
    files: ['**/*.ts'],
    extends: [
      ...tseslint.configs.strictTypeChecked,
      ...tseslint.configs.stylisticTypeChecked,
      ...angular.configs.tsRecommended,
    ],
    languageOptions: {
      parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname },
    },
    processor: angular.processInlineTemplates,
    rules: {
      eqeqeq: ['error', 'always'],
      // A component or directive class is often empty on purpose: the decorator is the whole point.
      '@typescript-eslint/no-extraneous-class': ['error', { allowWithDecorator: true }],
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-non-null-assertion': 'error',
      '@typescript-eslint/no-deprecated': 'error',
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: 'hr', style: 'kebab-case' },
      ],
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: 'hr', style: 'camelCase' },
      ],
      '@angular-eslint/prefer-on-push-component-change-detection': 'error',
      '@angular-eslint/prefer-standalone': 'error',
      '@angular-eslint/prefer-inject': 'error',
      // No HTML sinks, ever.
      'no-restricted-properties': [
        'error',
        { property: 'innerHTML', message: 'No HTML sinks: render text, never markup.' },
        { property: 'outerHTML', message: 'No HTML sinks.' },
        { property: 'insertAdjacentHTML', message: 'No HTML sinks.' },
        { property: 'bypassSecurityTrustHtml', message: 'Never bypass sanitization.' },
        { property: 'bypassSecurityTrustScript', message: 'Never bypass sanitization.' },
        { property: 'bypassSecurityTrustStyle', message: 'Never bypass sanitization.' },
        { property: 'bypassSecurityTrustUrl', message: 'Never bypass sanitization.' },
        { property: 'bypassSecurityTrustResourceUrl', message: 'Never bypass sanitization.' },
        { object: 'document', property: 'write', message: 'No HTML sinks.' },
      ],
      'no-eval': 'error',
      'no-implied-eval': 'error',
      'no-new-func': 'error',
      // Nothing from these packages reaches the browser; a library is entered only through its index.
      'no-restricted-imports': [
        'error',
        {
          paths: [
            'zone.js',
            '@angular/animations',
            '@angular/material',
            '@angular/platform-server',
          ],
          patterns: ['@harmony/*/*/*', 'rxjs/internal/*'],
        },
      ],
    },
  },
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
    rules: {
      '@angular-eslint/template/no-inline-styles': 'error',
    },
  },
);
