import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import QRCode from 'qrcode'
import { addPasskey, confirmIdentity, confirmIdentityOptions, disableTwoFactor, enableTwoFactor, identityConfirmationRequired, logoutEverywhere, myAccount, myPasskeys, passkeyOptions, removePasskey, renamePasskey, sessionExpired, setupTwoFactor, type AccountModel, type PasskeyModel, type TwoFactorSetup } from '../api'
import { messagesFor } from '../errors'
import { createPasskey, passkeyErrorMessage, passkeysSupported, signalPasskeyOwner, signalPasskeys, answerWithPasskey } from '../passkeys'
import { useSession } from '../session'
import { signInMethod } from '../signInMethod'
import { toast } from '../components/Toast'

/** What the person was doing when the API asked for a fresh proof of identity; done again once it is given. */
type Pending = { kind: 'add' } | { kind: 'remove'; passkey: PasskeyModel }

/**
 * The signed-in person's security: passkeys (made on this device or in a password manager; each signs in on its own),
 * two-step sign-in (set up with an authenticator app, recovery codes shown once, turn off with the password) and the
 * sessions (sign out everywhere). The account row is read when the role may read accounts.
 *
 * Adding or removing a passkey asks the person to prove it is them first (the password, or one of their passkeys),
 * because a session may be hours old and a passkey is a permanent key. Removing one ends every session, this one
 * included: the person signs in again once, and is told to delete the key from the device too.
 */
export default function SecurityPage() {
  const navigate = useNavigate()
  const { user } = useSession()
  const sub = user?.sub ?? ''
  const [account, setAccount] = useState<AccountModel | null>(null)
  const [canRead, setCanRead] = useState(true)
  const [setup, setSetup] = useState<TwoFactorSetup | null>(null)
  const [code, setCode] = useState('')
  const [recovery, setRecovery] = useState<string[] | null>(null)
  const [disabling, setDisabling] = useState(false)
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [passkeys, setPasskeys] = useState<PasskeyModel[] | null>(null)
  const [naming, setNaming] = useState(false)
  const [passkeyName, setPasskeyName] = useState('')
  const [passkeyError, setPasskeyError] = useState<string | null>(null)
  const [passkeyBusy, setPasskeyBusy] = useState(false)
  const [renaming, setRenaming] = useState<PasskeyModel | null>(null)
  const [renameTo, setRenameTo] = useState('')
  const [removing, setRemoving] = useState<PasskeyModel | null>(null)
  const [removed, setRemoved] = useState<PasskeyModel | null>(null)
  const [pending, setPending] = useState<Pending | null>(null)
  const [confirmPassword, setConfirmPassword] = useState('')
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const supported = passkeysSupported()

  async function loadAccount() {
    if (!sub) return
    const r = await myAccount(sub)
    if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
    if (r.ok && r.data) { setAccount(r.data); setCanRead(true) }
    else setCanRead(false)
  }

  async function loadPasskeys() {
    const r = await myPasskeys()
    if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
    const list = r.ok && r.data ? r.data : []
    setPasskeys(list)
    // The device learns which keys this server still holds (a key removed elsewhere is dropped here too), and the
    // name it shows next to them.
    if (list.length > 0 && sub) {
      signalPasskeys(list[0].rpId, sub, list.map((p) => p.credentialId))
      signalPasskeyOwner(list[0].rpId, sub, user?.name ?? '')
    }
  }

  useEffect(() => { void loadAccount(); void loadPasskeys() }, []) // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (setup && canvasRef.current) {
      void QRCode.toCanvas(canvasRef.current, setup.authenticatorUri, { width: 180, margin: 1 })
    }
  }, [setup])

  async function onSetup() {
    setBusy(true); setError(null)
    const r = await setupTwoFactor()
    setBusy(false)
    if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
    if (!r.ok || !r.data) { setError(messagesFor(r.codes)); return }
    setSetup(r.data)
    setCode('')
  }

  async function onEnable(event: FormEvent) {
    event.preventDefault()
    setBusy(true); setError(null)
    const r = await enableTwoFactor(code.trim())
    setBusy(false)
    if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
    if (!r.ok || !r.data) { setError(messagesFor(r.codes)); return }
    setRecovery(r.data.recoveryCodes)
    setSetup(null)
    toast('Two-step sign-in is on.')
    void loadAccount()
  }

  async function onDisable(event: FormEvent) {
    event.preventDefault()
    setBusy(true); setError(null)
    const r = await disableTwoFactor(password)
    setBusy(false)
    if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
    if (!r.ok) { setError(messagesFor(r.codes)); return }
    setDisabling(false)
    setPassword('')
    setRecovery(null)
    toast('Two-step sign-in is off.')
    void loadAccount()
  }

  /**
   * Adding a passkey: the API hands out the challenge, the browser makes the key (the person unlocks it with their
   * face, fingerprint or PIN), and the public half goes back under the name they chose, or none: the key is then named
   * after its maker and the rename opens at once, so the person can call it what they like.
   */
  async function onAddPasskey(event?: FormEvent) {
    event?.preventDefault()
    setPasskeyBusy(true); setPasskeyError(null)
    try {
      const options = await passkeyOptions()
      if (options.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
      if (options.codes.includes(identityConfirmationRequired)) { setPending({ kind: 'add' }); return }
      if (!options.ok || !options.data) { setPasskeyError(messagesFor(options.codes)); return }
      let credentialJson: string
      try {
        credentialJson = await createPasskey(options.data.optionsJson)
      } catch (browserError) {
        setPasskeyError(passkeyErrorMessage(browserError))
        return
      }
      const r = await addPasskey(passkeyName.trim(), credentialJson)
      if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
      if (r.codes.includes(identityConfirmationRequired)) { setPending({ kind: 'add' }); return }
      if (!r.ok || !r.data) { setPasskeyError(messagesFor(r.codes)); return }
      setNaming(false)
      setPasskeyName('')
      toast('The passkey was added. It signs you in on its own from now on.')
      if (!passkeyName.trim()) { setRenaming(r.data); setRenameTo(r.data.name) }
      void loadPasskeys()
    } finally {
      setPasskeyBusy(false)
    }
  }

  async function onRenamePasskey(event: FormEvent) {
    event.preventDefault()
    if (!renaming) return
    setPasskeyBusy(true); setPasskeyError(null)
    const r = await renamePasskey(renaming.id, renameTo.trim())
    setPasskeyBusy(false)
    if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
    if (!r.ok) { setPasskeyError(messagesFor(r.codes)); return }
    setRenaming(null)
    toast('The passkey was renamed.')
    void loadPasskeys()
  }

  /**
   * Removing a passkey (the FIDO Alliance's pattern): a confirmation naming the key, a warning when it is the last one,
   * then "Passkey removed" with the advice to delete it from the device too. The device is told at once (the signal),
   * and every session ends, so the next step is the sign-in page.
   */
  async function onRemovePasskey(passkey: PasskeyModel) {
    setPasskeyBusy(true); setPasskeyError(null)
    const r = await removePasskey(passkey.id)
    setPasskeyBusy(false)
    if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
    if (r.codes.includes(identityConfirmationRequired)) { setPending({ kind: 'remove', passkey }); return }
    if (!r.ok) { setPasskeyError(messagesFor(r.codes)); return }
    const left = (passkeys ?? []).filter((p) => p.id !== passkey.id)
    if (sub) signalPasskeys(passkey.rpId, sub, left.map((p) => p.credentialId))
    setRemoving(null)
    setPasskeys(left)
    setRemoved(passkey)
  }

  /** The proof the API asked for, with the password; then the interrupted action runs again. */
  async function onConfirmWithPassword(event: FormEvent) {
    event.preventDefault()
    setPasskeyBusy(true); setPasskeyError(null)
    const r = await confirmIdentity({ password: confirmPassword })
    setPasskeyBusy(false)
    if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
    if (!r.ok) { setPasskeyError(messagesFor(r.codes)); return }
    setConfirmPassword('')
    await resume()
  }

  /** The same proof with one of the person's passkeys: the API names them, the device signs the challenge. */
  async function onConfirmWithPasskey() {
    setPasskeyBusy(true); setPasskeyError(null)
    try {
      const options = await confirmIdentityOptions()
      if (options.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
      if (!options.ok || !options.data) { setPasskeyError(messagesFor(options.codes)); return }
      let credentialJson: string
      try {
        credentialJson = await answerWithPasskey(options.data.optionsJson)
      } catch (browserError) {
        setPasskeyError(passkeyErrorMessage(browserError))
        return
      }
      const r = await confirmIdentity({ credentialJson })
      if (r.codes.includes(sessionExpired)) { navigate('/login', { replace: true }); return }
      if (!r.ok) { setPasskeyError(messagesFor(r.codes)); return }
    } finally {
      setPasskeyBusy(false)
    }
    await resume()
  }

  async function resume() {
    const action = pending
    setPending(null)
    if (action?.kind === 'add') await onAddPasskey()
    if (action?.kind === 'remove') await onRemovePasskey(action.passkey)
  }

  /** Identity ends every session on every device, this one included (the BFF hears it server to server); then sign in again. */
  async function onSignOutEverywhere() {
    await logoutEverywhere()
    window.location.assign('/login')
  }

  const enabled = account?.twoFactorEnabled ?? null
  const when = (iso: string | null) => (iso ? new Date(iso).toLocaleDateString() : 'never')
  const lastOne = (passkeys?.length ?? 0) === 1

  return (
    <>
      <header className="page">
        <div>
          <h1>Security</h1>
          <p className="lead">Passkeys, two-step sign-in, your sessions and your password.</p>
        </div>
      </header>

      <div className="stack">
        <section className="surface pad">
          <h2>Passkeys</h2>
          <p className="lead">
            A passkey signs you in with your face, fingerprint or PIN instead of the password and the code. It cannot be
            phished: it only works on Harmony's own address. Add one on each device you use, or in your password manager.
            Not on a shared front-desk computer: anyone who can unlock it could sign in as you.
          </p>

          {removed && (
            <div className="ok" role="status">
              <p style={{ margin: 0 }}><strong>Passkey removed.</strong> "{removed.name}" cannot sign you in any more, and every session of your account was ended, this one included.</p>
              <p style={{ margin: '8px 0 0' }}>Delete the passkey from that device or password manager too (its Settings, Passkeys), so it stops being offered. Then sign in again{lastOne || (passkeys ?? []).length === 0 ? ' with your password' : ' with another passkey or your password'}.</p>
              <button type="button" className="auto" style={{ marginTop: 12 }} onClick={() => window.location.assign(user?.logoutUrl ?? '/login')}>Sign in again</button>
            </div>
          )}

          {!removed && passkeys === null && <p className="lead">Checking…</p>}
          {!removed && passkeys !== null && passkeys.length === 0 && <p className="lead">You have no passkeys yet.</p>}
          {!removed && passkeys !== null && passkeys.length > 0 && (
            <ul className="facts">
              {passkeys.map((p) => (
                <li key={p.id}>
                  {renaming?.id === p.id ? (
                    <form onSubmit={onRenamePasskey} style={{ display: 'flex', gap: 8, alignItems: 'center', width: '100%' }}>
                      <input aria-label="New name for the passkey" autoFocus maxLength={64} value={renameTo} onChange={(e) => setRenameTo(e.target.value)} style={{ flex: 1 }} />
                      <button type="submit" className="auto" disabled={passkeyBusy || !renameTo.trim()}>Save</button>
                      <button type="button" className="secondary auto" disabled={passkeyBusy} onClick={() => setRenaming(null)}>Cancel</button>
                    </form>
                  ) : (
                    <>
                      <span>{p.name}</span>
                      <span style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                        <span className="lead" style={{ margin: 0 }}>
                          {p.isBackedUp ? 'synced' : 'on one device'} · added {when(p.createdUtc)} · last used {when(p.lastUsedUtc)}
                        </span>
                        <button type="button" className="secondary auto" disabled={passkeyBusy} onClick={() => { setRenaming(p); setRenameTo(p.name); setPasskeyError(null) }}>Rename</button>
                        <button type="button" className="secondary auto" disabled={passkeyBusy} onClick={() => { setRemoving(p); setPending(null); setPasskeyError(null) }}>Remove</button>
                      </span>
                    </>
                  )}
                </li>
              ))}
            </ul>
          )}
          {!removed && passkeys !== null && passkeys.length > 0 && passkeys.every((p) => !p.isBackedUp) && (
            <p className="lead" style={{ marginTop: 10 }}>Your passkeys live on one device each: a lost device takes its passkey with it. Keep a second one, or your password, as the way back in.</p>
          )}

          {removing && !pending && (
            <div className="surface pad" style={{ marginTop: 14 }} role="alertdialog" aria-labelledby="remove-title">
              <h3 id="remove-title" style={{ marginTop: 0 }}>Remove the passkey "{removing.name}"?</h3>
              <p className="lead">It will not sign you in any more. Every session of your account ends, this one included, and you sign in again once.</p>
              {lastOne && <p className="lead">This is your only passkey. Afterwards you sign in with your password and the two-step code.</p>}
              <div style={{ display: 'flex', gap: 10 }}>
                <button type="button" className="danger auto" disabled={passkeyBusy} onClick={() => { void onRemovePasskey(removing) }}>Remove</button>
                <button type="button" className="secondary auto" disabled={passkeyBusy} onClick={() => { setRemoving(null); setPasskeyError(null) }}>Keep it</button>
              </div>
            </div>
          )}

          {pending && (
            <form onSubmit={onConfirmWithPassword} className="surface pad" style={{ marginTop: 14, maxWidth: 460 }}>
              <h3 style={{ marginTop: 0 }}>Confirm it is you</h3>
              <p className="lead">{pending.kind === 'add' ? 'Adding a passkey' : 'Removing a passkey'} changes how your account is entered, so prove it is you first: your password, or one of your passkeys. The proof is good for 5 minutes.</p>
              <label htmlFor="confirm-pw">Your password</label>
              <input id="confirm-pw" type="password" autoComplete="current-password" autoFocus value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} />
              <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
                <button type="submit" disabled={passkeyBusy || !confirmPassword}>Confirm</button>
                {supported && (passkeys?.length ?? 0) > 0 && <button type="button" className="secondary" disabled={passkeyBusy} onClick={() => { void onConfirmWithPasskey() }}>Use a passkey instead</button>}
                <button type="button" className="secondary" disabled={passkeyBusy} onClick={() => { setPending(null); setConfirmPassword(''); setPasskeyError(null) }}>Cancel</button>
              </div>
            </form>
          )}

          {!removed && !supported && <p className="lead">This browser cannot make passkeys. Use a current Chrome, Edge, Safari or Firefox.</p>}

          {!removed && supported && !naming && !pending && (
            <div style={{ display: 'flex', gap: 10, marginTop: 14, flexWrap: 'wrap' }}>
              <button type="button" className="auto" disabled={passkeyBusy} onClick={() => { setNaming(true); setRemoving(null); setPasskeyError(null) }}>Add a passkey</button>
            </div>
          )}

          {!removed && supported && naming && !pending && (
            <form onSubmit={onAddPasskey} style={{ maxWidth: 420 }}>
              <label htmlFor="passkey-name">A name for this passkey (optional: without one it is named after its maker, like "Windows Hello")</label>
              <input id="passkey-name" autoFocus maxLength={64} placeholder="Work laptop" value={passkeyName} onChange={(e) => setPasskeyName(e.target.value)} />
              <div style={{ display: 'flex', gap: 10 }}>
                <button type="submit" disabled={passkeyBusy}>{passkeyBusy ? 'Waiting for your device…' : 'Create the passkey'}</button>
                <button type="button" className="secondary" disabled={passkeyBusy} onClick={() => { setNaming(false); setPasskeyError(null) }}>Cancel</button>
              </div>
            </form>
          )}

          {passkeyError && <p className="error" role="alert">{passkeyError}</p>}
        </section>

        <section className="surface pad">
          <h2>Two-step sign-in</h2>
          <p className="lead">
            {enabled === true && 'On. Signing in with the password asks for a 6-digit code from your authenticator app.'}
            {enabled === false && 'Off. Turn it on so a stolen password alone cannot sign in as you.'}
            {enabled === null && (canRead ? 'Checking…' : 'Your role cannot read account details, so the current state is not shown here; the actions below still work.')}
          </p>

          {recovery && (
            <div>
              <p className="ok">Save these recovery codes now. Each works once, for the day the phone is lost; they are not shown again.</p>
              <div className="codes">{recovery.map((c) => <code key={c}>{c}</code>)}</div>
              <button type="button" className="secondary auto" style={{ marginTop: 12 }} onClick={() => { void navigator.clipboard?.writeText(recovery.join('\n')); toast('Recovery codes copied.') }}>Copy the codes</button>
            </div>
          )}

          {!setup && !disabling && (
            <div style={{ display: 'flex', gap: 10, marginTop: 14, flexWrap: 'wrap' }}>
              {enabled !== true && <button type="button" className="auto" disabled={busy} onClick={onSetup}>Set up an authenticator app</button>}
              {enabled !== false && <button type="button" className="auto secondary" disabled={busy} onClick={() => { setDisabling(true); setError(null) }}>Turn off two-step sign-in</button>}
            </div>
          )}

          {setup && (
            <form onSubmit={onEnable}>
              <div className="qr">
                <canvas ref={canvasRef} width={180} height={180} aria-label="QR code for the authenticator app" />
                <div style={{ flex: 1, minWidth: 220 }}>
                  <p className="lead" style={{ marginBottom: 6 }}>Scan the code with Microsoft Authenticator, Google Authenticator or any TOTP app, or type the key:</p>
                  <p className="mono" style={{ wordBreak: 'break-all', margin: 0 }}>{setup.sharedKey}</p>
                  <label htmlFor="otp">Then enter the 6-digit code the app shows</label>
                  <input id="otp" inputMode="numeric" autoComplete="one-time-code" autoFocus value={code} onChange={(e) => setCode(e.target.value)} />
                  {error && <p className="error" role="alert">{error}</p>}
                  <div style={{ display: 'flex', gap: 10 }}>
                    <button type="submit" disabled={busy || code.trim().length !== 6}>Turn on</button>
                    <button type="button" className="secondary" onClick={() => { setSetup(null); setError(null) }}>Cancel</button>
                  </div>
                </div>
              </div>
            </form>
          )}

          {disabling && (
            <form onSubmit={onDisable} style={{ maxWidth: 420 }}>
              <label htmlFor="pw">Your current password, to confirm</label>
              <input id="pw" type="password" autoComplete="current-password" autoFocus value={password} onChange={(e) => setPassword(e.target.value)} />
              {error && <p className="error" role="alert">{error}</p>}
              <div style={{ display: 'flex', gap: 10 }}>
                <button type="submit" className="danger" disabled={busy || !password}>Turn off</button>
                <button type="button" className="secondary" onClick={() => { setDisabling(false); setError(null); setPassword('') }}>Cancel</button>
              </div>
            </form>
          )}

          {!setup && !disabling && error && <p className="error" role="alert">{error}</p>}
        </section>

        <section className="surface pad">
          <h2>Sessions</h2>
          <p className="lead">Signing out everywhere ends every session on every device, this one included, and invalidates every refresh token.</p>
          <ul className="facts">
            <li><span>Signed in as</span><span>{user?.name ?? ''}</span></li>
            <li><span>Signed in with</span><span>{signInMethod(user?.amr)}</span></li>
            <li><span>Security version</span><span>{String(account?.securityVersion ?? '')}</span></li>
          </ul>
          <div style={{ display: 'flex', gap: 10, marginTop: 14, flexWrap: 'wrap' }}>
            <button type="button" className="auto secondary" onClick={() => navigate('/account/change-password')}>Change password</button>
            <button type="button" className="auto danger" onClick={onSignOutEverywhere}>Sign out everywhere</button>
          </div>
        </section>
      </div>
    </>
  )
}
