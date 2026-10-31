// The sign-in pages send each page once. After the first send (a click, or Enter), every later send on the page is
// ignored: a double-click, Enter and a click together, a second button. The buttons show they are working.
// Nothing is disabled, so the clicked button still sends its own value; nothing is built from strings, so the strict
// Content Security Policy and Trusted Types stay on. The server still handles a repeat safely: this only spares the
// person a confusing page.
(() => {
    'use strict';

    // A send that never answered (the network dropped it): let the person try again.
    const unlockAfterMs = 15000;
    const page = document.documentElement;

    function buttonsOf(form) {
        return form.querySelectorAll('button:not([type]), button[type="submit"]');
    }

    function lock() {
        page.dataset.sending = 'true';
        for (const form of document.forms) {
            form.setAttribute('aria-busy', 'true');
            for (const button of buttonsOf(form)) {
                button.setAttribute('aria-disabled', 'true');
                if (button.dataset.busy) {
                    button.dataset.label = button.textContent;
                    button.textContent = button.dataset.busy;
                }
            }
        }

        window.setTimeout(unlock, unlockAfterMs);
    }

    function unlock() {
        delete page.dataset.sending;
        for (const form of document.forms) {
            form.removeAttribute('aria-busy');
            for (const button of buttonsOf(form)) {
                button.removeAttribute('aria-disabled');
                if (button.dataset.label !== undefined) {
                    button.textContent = button.dataset.label;
                    delete button.dataset.label;
                }
            }
        }
    }

    document.addEventListener('submit', (event) => {
        if (page.dataset.sending === 'true') {
            event.preventDefault();
            return;
        }

        lock();
    });

    // Back to this page from the browser's back/forward memory: it must not stay locked.
    window.addEventListener('pageshow', (event) => {
        if (event.persisted) {
            unlock();
        }
    });
})();

// Passkeys on the sign-in page (WebAuthn). The page asks this host for a challenge, the browser's authenticator signs it
// (the person's face, fingerprint or PIN unlocks the key), and the signed answer goes back as an ordinary form send.
// Only the button starts it: a request made on page load ("conditional" mediation, the browser's autofill offer) turns
// into a modal system dialog in browsers that claim the feature without the autofill UI (embedded Chromium).
// Nothing here builds markup or runs strings, so the strict policy and Trusted Types stay on.
(() => {
    'use strict';

    const form = document.getElementById('passkey-form');
    if (!form || !window.PublicKeyCredential || typeof PublicKeyCredential.parseRequestOptionsFromJSON !== 'function') {
        return;
    }

    const button = document.getElementById('passkey-button');
    const credential = document.getElementById('passkey-credential');
    const message = document.getElementById('passkey-error');
    const tokenField = form.querySelector('input[name="__RequestVerificationToken"]');
    const idle = button.textContent;
    let pending = null;

    async function challenge(signal) {
        const response = await fetch(form.dataset.options, {
            method: 'POST',
            headers: { 'RequestVerificationToken': tokenField ? tokenField.value : '', 'Accept': 'application/json' },
            signal,
        });
        if (!response.ok) {
            throw new Error('challenge ' + response.status);
        }

        return PublicKeyCredential.parseRequestOptionsFromJSON(await response.json());
    }

    function say(text) {
        message.textContent = text;
        message.hidden = !text;
    }

    function base64Url(bytes) {
        if (!bytes) {
            return undefined;
        }

        // A Uint8Array over a buffer or a copy of a view: no instanceof, which lies across realms.
        const view = new Uint8Array(bytes);
        let text = '';
        for (let i = 0; i < view.byteLength; i++) {
            text += String.fromCharCode(view[i]);
        }

        return window.btoa(text).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
    }

    // The answer as the JSON the server reads: the shape of PublicKeyCredential.toJSON(), built by hand, field by field,
    // for every browser alike. Never the browser's own toJSON(): some password managers break it ("Illegal invocation")
    // or leave fields out of it (the cases Microsoft documents), and a hand-built answer cannot.
    function serialize(answer) {
        return JSON.stringify({
            id: answer.id,
            rawId: base64Url(answer.rawId),
            type: answer.type,
            authenticatorAttachment: answer.authenticatorAttachment || undefined,
            clientExtensionResults: answer.getClientExtensionResults(),
            response: {
                clientDataJSON: base64Url(answer.response.clientDataJSON),
                authenticatorData: base64Url(answer.response.authenticatorData),
                signature: base64Url(answer.response.signature),
                userHandle: base64Url(answer.response.userHandle),
            },
        });
    }

    // One request at a time: a second click while the dialog is open is ignored.
    async function signIn() {
        if (pending) {
            return;
        }

        const controller = new AbortController();
        pending = controller;
        say('');
        button.setAttribute('aria-disabled', 'true');
        button.textContent = 'Waiting for your passkey…';

        try {
            const publicKey = await challenge(controller.signal);
            const answer = await navigator.credentials.get({ publicKey, signal: controller.signal });
            if (!answer) {
                return;
            }

            credential.value = serialize(answer);
            form.requestSubmit();
        } catch (error) {
            if (error && error.name === 'AbortError') {
                return;
            }

            say(error && error.name === 'NotAllowedError'
                ? 'No passkey was used. Try again, or sign in with your password.'
                : 'The passkey could not be used here. Sign in with your password.');
        } finally {
            pending = null;
            button.removeAttribute('aria-disabled');
            button.textContent = idle;
        }
    }

    form.hidden = false;
    button.addEventListener('click', signIn);

    // A key this server no longer knows (the last answer named it): the browser is told, so the device stops offering a
    // dead key (WebAuthn Signal API: Chrome and Edge 132+, Safari 26; a browser without the method is simply not told).
    const unknown = document.getElementById('passkey-unknown');
    if (unknown && typeof PublicKeyCredential.signalUnknownCredential === 'function') {
        PublicKeyCredential.signalUnknownCredential({ rpId: unknown.dataset.rpId, credentialId: unknown.dataset.credentialId }).catch(() => undefined);
    }
})();

// The refusal card's Copy (console frame, C12b): the reference goes to the clipboard, the button says so for a moment.
// Nothing here builds markup, so the strict policy and Trusted Types stay on.
(() => {
    'use strict';

    const button = document.getElementById('copy-reference');
    if (!button || !navigator.clipboard) {
        return;
    }

    const label = button.textContent;
    button.addEventListener('click', async () => {
        try {
            await navigator.clipboard.writeText(button.dataset.copy || '');
            button.textContent = button.dataset.done || label;
            window.setTimeout(() => { button.textContent = label; }, 1500);
        } catch {
            // The browser refused the clipboard: the reference stays readable beside the button.
        }
    });
})();
