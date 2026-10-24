import { ChangeDetectionStrategy, Component, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ConfirmDialog } from './confirm-dialog';

const tick = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 1));

@Component({
  selector: 'hr-host',
  imports: [ConfirmDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hr-confirm-dialog
      #leave
      heading="Sign in on this page?"
      message="Changes not saved will be lost."
      go="Sign in here"
      stay="Stay"
      tone="stop"
    />
  `,
})
class Host {
  readonly leave = viewChild.required(ConfirmDialog);
}

/** jsdom has no dialog API: the component falls back to the open attribute, which is what these specs read. */
const isOpen = (dialog: HTMLDialogElement): boolean => dialog.hasAttribute('open');

describe('ConfirmDialog', () => {
  it('asks with the heading and message, the safe button first; Go answers true, Stay answers false, a close (Escape) answers false', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;
    const dialog = page.querySelector<HTMLDialogElement>('dialog.dlg');
    expect(dialog).not.toBeNull();
    if (dialog === null) return;
    const ask = fixture.componentInstance.leave();

    let answer = ask.ask();
    expect(isOpen(dialog)).toBe(true);
    expect(page.querySelector('.dlg-h')?.textContent).toBe('Sign in on this page?');
    expect(dialog.getAttribute('aria-labelledby')).toBe(page.querySelector('.dlg-h')?.id);
    expect(page.querySelector('.dlg-m')?.textContent).toBe('Changes not saved will be lost.');
    const buttons = [...page.querySelectorAll<HTMLButtonElement>('.dlg-a .btn')];
    expect(buttons.map((button) => button.textContent.trim())).toEqual(['Stay', 'Sign in here']);
    expect(buttons[0]?.hasAttribute('autofocus')).toBe(true);
    expect(buttons[1]?.classList.contains('stop')).toBe(true);
    buttons[1]?.click();
    await tick();
    expect(await answer).toBe(true);
    expect(isOpen(dialog)).toBe(false);

    answer = ask.ask();
    buttons[0]?.click();
    await tick();
    expect(await answer).toBe(false);

    answer = ask.ask();
    // Escape: the browser closes the dialog itself and fires close; nothing was chosen.
    dialog.removeAttribute('open');
    dialog.dispatchEvent(new Event('close'));
    await tick();
    expect(await answer).toBe(false);
  });

  it('a second ask while one is open is answered false at once; the first keeps waiting for the person', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const ask = fixture.componentInstance.leave();
    const first = ask.ask();
    expect(await ask.ask()).toBe(false);
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('.dlg-a .solid')
      ?.click();
    await tick();
    expect(await first).toBe(true);
  });
});
