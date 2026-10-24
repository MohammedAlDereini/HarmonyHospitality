const PARTS_SHOWN = 2;

/** "Layla Al-Harbi" → "LA": the first letter of the first two words, in the name's own script, upper-cased. */
export const initialsOf = (name: string): string =>
  name
    .split(' ')
    .filter((part) => part !== '')
    .slice(0, PARTS_SHOWN)
    .map((part) => {
      const first = part.codePointAt(0);
      return first === undefined ? '' : String.fromCodePoint(first);
    })
    .join('')
    .toLocaleUpperCase();
