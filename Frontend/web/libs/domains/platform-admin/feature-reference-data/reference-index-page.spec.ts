import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ReferenceStore } from '@harmony/platform-admin/data-access';
import { ReferenceIndexPage } from './reference-index-page';

const CURRENCIES = 12;
const TIME_ZONES = 9;
const TABLES = 13;
const FILLED = 2;
/** Lookup values are read per category and never counted as a whole. */
const SCOPED = 1;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

/** Two tables hold rows, two are empty, the rest are counted as empty too (a fresh stand). */
const counts: Readonly<Record<string, number>> = { Currency: CURRENCIES, TimeZone: TIME_ZONES };

describe('ReferenceIndexPage', () => {
  let fixture: ComponentFixture<ReferenceIndexPage>;
  let page: HTMLElement;

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const text = (selector: string): string =>
    page.querySelector(selector)?.textContent.replace(whitespace, ' ').trim() ?? '';
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) =>
      cell.textContent.replace(whitespace, ' ').trim(),
    );

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: ReferenceStore,
          useValue: {
            count: (controller: string) => ({ total: signal(counts[controller] ?? 0) }),
          },
        },
      ],
    });
    fixture = TestBed.createComponent(ReferenceIndexPage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  });

  it('counts the tables and their rows in the lead, says what is still empty, and tiles every table with its count', () => {
    expect(text('.lead')).toBe(
      `${String(TABLES)} shared tables · kept by the platform team · ${String(CURRENCIES + TIME_ZONES)} rows · a hotel only reads them`,
    );
    expect(text('.c-filters .count')).toBe(
      `${String(TABLES)} tables · ${String(TABLES - FILLED - SCOPED)} still empty`,
    );
    expect(text('.c-msg b')).toBe(`Still empty: ${String(TABLES - FILLED - SCOPED)} tables`);
    expect(page.querySelectorAll('a.tile').length).toBe(TABLES);
    expect(texts('a.tile .t').slice(0, FILLED)).toEqual(['Currencies', 'Time zones']);
    expect(texts('a.tile .n').slice(0, FILLED)).toEqual([String(CURRENCIES), String(TIME_ZONES)]);
    expect(page.querySelectorAll('a.tile .n.zero').length).toBe(TABLES - FILLED - SCOPED);
    expect(page.querySelector('a.tile')?.getAttribute('href')).toBe('/currencies');
  });

  it('the search narrows the tiles', async () => {
    const box = page.querySelector<HTMLInputElement>('#tables-search');
    if (box === null) throw new Error('no search');
    box.value = 'zone';
    box.dispatchEvent(new Event('input'));
    await settle();
    expect(texts('a.tile .t')).toEqual(['Time zones']);
  });
});
