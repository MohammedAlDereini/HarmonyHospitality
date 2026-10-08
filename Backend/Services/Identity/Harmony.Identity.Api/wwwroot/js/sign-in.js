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
