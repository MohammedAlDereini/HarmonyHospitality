import { errorMessage, kindMessage, messageOf } from './error-messages';

const FORBIDDEN = 403;
const UNAUTHORIZED = 401;
const UNPROCESSABLE = 422;

describe('error wording', () => {
  it('a known code has its own sentence', () => {
    expect(errorMessage('000.06')).toContain('Still working');
  });

  it('an unknown code has none, so the kind speaks', () => {
    expect(errorMessage('999.99')).toBeUndefined();
    expect(messageOf({ kind: 'forbidden', status: FORBIDDEN, codes: ['999.99'] })).toBe(
      kindMessage('forbidden'),
    );
  });

  it('the first code with wording wins', () => {
    expect(
      messageOf({ kind: 'conflict', status: UNPROCESSABLE, codes: ['999.99', '000.07'] }),
    ).toContain('already used');
  });

  it('an outcome without codes uses the kind', () => {
    expect(messageOf({ kind: 'unauthenticated', status: UNAUTHORIZED })).toBe(
      kindMessage('unauthenticated'),
    );
  });
});
