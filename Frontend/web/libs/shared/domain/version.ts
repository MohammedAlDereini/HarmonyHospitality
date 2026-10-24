/**
 * The build's own version, as the deployment's floor (minimumClientVersion) is compared against it. Stamped by
 * tools/stamp-version.mjs before a release build; a development build runs as 0.0.0.
 */
export const APP_VERSION = '0.0.0';

/** Compares two dotted versions numerically, part by part; a missing part counts as 0. Negative: a is older. */
export const compareVersions = (a: string, b: string): number => {
  const parts = (text: string): number[] =>
    text
      .split('.')
      .map((part) => Number.parseInt(part, 10))
      .map((part) => (Number.isNaN(part) ? 0 : part));
  const left = parts(a);
  const right = parts(b);
  const length = Math.max(left.length, right.length);
  return (
    Array.from({ length }, (_, index) => (left[index] ?? 0) - (right[index] ?? 0)).find(
      (difference) => difference !== 0,
    ) ?? 0
  );
};
