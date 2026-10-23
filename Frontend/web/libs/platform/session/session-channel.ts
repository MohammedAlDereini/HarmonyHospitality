import { Injectable } from '@angular/core';

/** What the tabs of one origin tell each other (architecture 9 and 14: shared sign-out; the separate-window return). */
export type SessionMessage = { readonly type: 'signed-in' } | { readonly type: 'signed-out' };

export const sessionChannelName = 'harmony-session';

/** Only the two known messages cross; anything else on the channel is dropped. */
export const asSessionMessage = (data: unknown): SessionMessage | null => {
  if (typeof data !== 'object' || data === null) return null;
  const type: unknown = (data as { type?: unknown }).type;
  return type === 'signed-in' || type === 'signed-out' ? { type } : null;
};

/**
 * The tabs' channel. BroadcastChannel reaches the same origin only, so no origin check is needed (ASVS 3.5.5); a
 * browser without it simply has tabs that do not talk, each still obeying the server.
 */
@Injectable({ providedIn: 'root' })
export class SessionChannel {
  private readonly channel =
    typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel(sessionChannelName);

  post(message: SessionMessage): void {
    this.channel?.postMessage(message);
  }

  /** Messages from the other tabs; returns the stop function. */
  listen(handler: (message: SessionMessage) => void): () => void {
    const channel = this.channel;
    if (channel === null) return () => undefined;
    const onMessage = (event: MessageEvent<unknown>): void => {
      const message = asSessionMessage(event.data);
      if (message !== null) handler(message);
    };
    channel.addEventListener('message', onMessage);
    return () => {
      channel.removeEventListener('message', onMessage);
    };
  }
}
