import { csvCell, csvFile, csvLine } from './csv';

const ROOMS = 120;
const BELOW = -5;

describe('csv', () => {
  it('quotes only what needs quoting and doubles the quotes inside', () => {
    expect(csvCell('RITZ')).toBe('RITZ');
    expect(csvCell('Ritz, Amman')).toBe('"Ritz, Amman"');
    expect(csvCell('say "hi"')).toBe('"say ""hi"""');
    expect(csvCell('two\nlines')).toBe('"two\nlines"');
    expect(csvCell(null)).toBe('');
    expect(csvCell(ROOMS)).toBe('120');
  });

  it('keeps a typed text that a spreadsheet would run as a formula as text; numbers stay numbers', () => {
    expect(csvCell('=HYPERLINK("https://evil.example")')).toBe(
      '"\'=HYPERLINK(""https://evil.example"")"',
    );
    expect(csvCell('+cmd')).toBe("'+cmd");
    expect(csvCell('-1 star')).toBe("'-1 star");
    expect(csvCell('@sum')).toBe("'@sum");
    expect(csvCell('\tlead')).toBe("'\tlead");
    expect(csvCell(BELOW)).toBe('-5');
    expect(csvCell('Amman')).toBe('Amman');
  });

  it('joins cells with commas and lines with CRLF, the header first', () => {
    expect(csvLine(['a', 'b, c', undefined])).toBe('a,"b, c",');
    expect(csvFile(['Code', 'Rooms'], [['RITZ', ROOMS]])).toBe('Code,Rooms\r\nRITZ,120');
  });
});
