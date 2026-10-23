import { asSessionMessage } from './session-channel';

describe('asSessionMessage', () => {
  it('passes the two known messages', () => {
    expect(asSessionMessage({ type: 'signed-in' })).toEqual({ type: 'signed-in' });
    expect(asSessionMessage({ type: 'signed-out', extra: 1 })).toEqual({ type: 'signed-out' });
  });

  it('drops anything else', () => {
    expect(asSessionMessage({ type: 'reload' })).toBeNull();
    expect(asSessionMessage('signed-in')).toBeNull();
    expect(asSessionMessage(null)).toBeNull();
  });
});
