import { readRuntimeConfig, runtimeConfigElementId } from './runtime-config';

const emptyPage = (): Document => document.implementation.createHTMLDocument('test');

const pageWithElement = (json: string): Document => {
  const page = emptyPage();
  const element = page.createElement('script');
  element.id = runtimeConfigElementId;
  element.type = 'application/json';
  element.textContent = json;
  page.head.append(element);
  return page;
};

describe('readRuntimeConfig', () => {
  it('no element: the page runs as a local build', () => {
    expect(readRuntimeConfig(emptyPage())).toEqual({
      environment: 'local',
      release: 'local',
      minimumClientVersion: '0',
    });
  });

  it('reads the three fields and ignores anything else', () => {
    const page = pageWithElement(
      '{"environment":"Qa","release":"2026.10.23.1","minimumClientVersion":"2026.10.20.1","extra":true}',
    );
    expect(readRuntimeConfig(page)).toEqual({
      environment: 'Qa',
      release: '2026.10.23.1',
      minimumClientVersion: '2026.10.20.1',
    });
  });

  it('a missing or empty field stops the page', () => {
    expect(() => readRuntimeConfig(pageWithElement('{"environment":"Qa","release":""}'))).toThrow(
      'release is missing or empty',
    );
  });

  it('broken JSON stops the page', () => {
    expect(() => readRuntimeConfig(pageWithElement('{not json'))).toThrow('not valid JSON');
  });

  it('a JSON value that is not an object stops the page', () => {
    expect(() => readRuntimeConfig(pageWithElement('"text"'))).toThrow('not an object');
  });
});
