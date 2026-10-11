/**
 * Passkeys in the browser (WebAuthn). Making one: the API gives the creation options (the challenge, who the key is
 * for); the browser's authenticator makes the key pair and the person unlocks it with their face, fingerprint or PIN;
 * the public half goes back to the API as the credential JSON. Using one to prove it is you: the API gives the request
 * options naming the person's own keys; the device signs the challenge. The private half never leaves the device or
 * the person's password manager. The signals tell the device which keys this server still knows.
 */

/** True when this browser can make and use passkeys with the JSON helpers the API speaks. */
export function passkeysSupported(): boolean {
  return typeof window !== 'undefined'
    && 'PublicKeyCredential' in window
    && typeof PublicKeyCredential.parseCreationOptionsFromJSON === 'function'
    && typeof PublicKeyCredential.parseRequestOptionsFromJSON === 'function'
}

/** The browser's answer to the creation options, as the JSON the API expects. Throws what the browser throws. */
export async function createPasskey(optionsJson: string): Promise<string> {
  const publicKey = PublicKeyCredential.parseCreationOptionsFromJSON(JSON.parse(optionsJson) as PublicKeyCredentialCreationOptionsJSON)
  const credential = await navigator.credentials.create({ publicKey })
  if (!credential) throw new DOMException('No passkey was created.', 'NotAllowedError')
  return serializeCreation(credential as PublicKeyCredential)
}

/** The browser's signed answer to the request options (one of the person's own keys), as the JSON the API expects. */
export async function answerWithPasskey(optionsJson: string): Promise<string> {
  const publicKey = PublicKeyCredential.parseRequestOptionsFromJSON(JSON.parse(optionsJson) as PublicKeyCredentialRequestOptionsJSON)
  const credential = await navigator.credentials.get({ publicKey })
  if (!credential) throw new DOMException('No passkey was used.', 'NotAllowedError')
  return serializeAssertion(credential as PublicKeyCredential)
}

/**
 * The JSON the API reads: the shape of PublicKeyCredential.toJSON(), built by hand, field by field, for every browser
 * alike. Never the browser's own toJSON(): some password managers break it ("Illegal invocation") or leave fields out
 * of it (the cases Microsoft documents), and a hand-built answer cannot.
 */
function serializeCreation(credential: PublicKeyCredential): string {
  const response = credential.response as AuthenticatorAttestationResponse
  return JSON.stringify({
    id: credential.id,
    rawId: base64Url(credential.rawId),
    type: credential.type,
    authenticatorAttachment: credential.authenticatorAttachment ?? undefined,
    clientExtensionResults: credential.getClientExtensionResults(),
    response: {
      clientDataJSON: base64Url(response.clientDataJSON),
      attestationObject: base64Url(response.attestationObject),
      transports: typeof response.getTransports === 'function' ? response.getTransports() : [],
    },
  })
}

function serializeAssertion(credential: PublicKeyCredential): string {
  const response = credential.response as AuthenticatorAssertionResponse
  return JSON.stringify({
    id: credential.id,
    rawId: base64Url(credential.rawId),
    type: credential.type,
    authenticatorAttachment: credential.authenticatorAttachment ?? undefined,
    clientExtensionResults: credential.getClientExtensionResults(),
    response: {
      clientDataJSON: base64Url(response.clientDataJSON),
      authenticatorData: base64Url(response.authenticatorData),
      signature: base64Url(response.signature),
      userHandle: base64Url(response.userHandle),
    },
  })
}

function base64Url(bytes: ArrayBuffer | null | undefined): string | undefined {
  if (!bytes) return undefined
  const view = new Uint8Array(bytes)
  let text = ''
  for (let i = 0; i < view.byteLength; i++) text += String.fromCharCode(view[i])
  return window.btoa(text).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')
}

/** The person's user handle as their passkeys carry it: the account id, base64url (what the API put in the creation options). */
function userHandleOf(sub: string): string {
  return window.btoa(sub).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')
}

type Signals = {
  signalAllAcceptedCredentials?: (options: { rpId: string; userId: string; allAcceptedCredentialIds: string[] }) => Promise<void>
  signalCurrentUserDetails?: (options: { rpId: string; userId: string; name: string; displayName: string }) => Promise<void>
}

/**
 * Tells the browser which passkeys this server still holds for the person (WebAuthn Signal API: Chrome and Edge 132+,
 * Safari 26): a removed key is then dropped from the device or the password manager too, so it is never offered again.
 * Called with the full list every time the list is known. A browser without the method is simply not told.
 */
export function signalPasskeys(rpId: string, sub: string, credentialIds: string[]): void {
  const signals = PublicKeyCredential as unknown as Signals
  if (typeof signals.signalAllAcceptedCredentials !== 'function') return
  void signals.signalAllAcceptedCredentials({ rpId, userId: userHandleOf(sub), allAcceptedCredentialIds: credentialIds }).catch(() => undefined)
}

/** Keeps the name the device shows next to the person's passkeys current (the same Signal API). */
export function signalPasskeyOwner(rpId: string, sub: string, name: string): void {
  const signals = PublicKeyCredential as unknown as Signals
  if (typeof signals.signalCurrentUserDetails !== 'function') return
  void signals.signalCurrentUserDetails({ rpId, userId: userHandleOf(sub), name, displayName: name }).catch(() => undefined)
}

/** What the person reads when the browser refused or the person cancelled. */
export function passkeyErrorMessage(error: unknown): string {
  const name = (error as { name?: string } | null)?.name
  if (name === 'NotAllowedError') return 'No passkey was used. Try again when you are ready.'
  if (name === 'InvalidStateError') return 'This device already holds a passkey for your account.'
  return 'This browser could not use a passkey here.'
}
