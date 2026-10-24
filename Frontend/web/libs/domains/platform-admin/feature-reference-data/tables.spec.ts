import { fieldForCode, tableByKey, tables } from './tables';

const TABLE_COUNT = 12;
// A URL segment: lower-case letters and dashes only. Matches "lookup-values"; rejects "LookupValues".
// Tester: https://regex101.com/?regex=%5E%5Ba-z-%5D%2B%24&testString=lookup-values
const segment = /^[a-z-]+$/;
// A controller name as Identity spells it: a capital, then letters. Matches "LookupValue"; rejects "lookup-value".
// Tester: https://regex101.com/?regex=%5E%5BA-Z%5D%5BA-Za-z%5D%2B%24&testString=LookupValue
const controllerName = /^[A-Z][A-Za-z]+$/;

describe('the reference tables', () => {
  it('names twelve tables, each with a key field or an id, columns the rows carry, and a URL segment', () => {
    expect(tables).toHaveLength(TABLE_COUNT);
    tables.forEach((table) => {
      expect(table.key).toMatch(segment);
      expect(table.controller).toMatch(controllerName);
      const keyField = table.fields.find((field) => field.isKey);
      expect(keyField?.key ?? table.idField).toBeTruthy();
      expect(table.columns.length).toBeGreaterThan(0);
      expect(new Set(table.fields.map((field) => field.key)).size).toBe(table.fields.length);
    });
    expect(tableByKey('currencies')?.controller).toBe('Currency');
    expect(tableByKey('no-such-table')).toBeNull();
  });

  it("maps Identity's codes onto the fields a table has, and onto nothing it lacks", () => {
    const currencies = tableByKey('currencies');
    const modules = tableByKey('modules');
    if (currencies === null || modules === null) throw new Error('tables missing');
    expect(fieldForCode(currencies, '10402')).toBe('code');
    expect(fieldForCode(currencies, '00056')).toBe('code');
    expect(fieldForCode(currencies, '10408')).toBe('nameAr');
    expect(fieldForCode(modules, '10408')).toBeNull();
    expect(fieldForCode(modules, '10410')).toBe('ownerService');
    expect(fieldForCode(currencies, '00042')).toBeNull();
  });
});
