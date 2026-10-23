import { ErrorHandler } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RUNTIME_CONFIG } from '@harmony/platform/config';
import { ERROR_REPORT_SINK, ErrorReport } from './error-report';
import { FailureNotice } from './failure-notice';
import { REFERENCE_LENGTH } from './harmony-error-handler';
import { provideHarmonyErrors } from './provide';

const REPORT_CAP = 20;
const A_FEW_MORE = 2;
const BEYOND_THE_CAP = REPORT_CAP + A_FEW_MORE;

describe('HarmonyErrorHandler', () => {
  let handler: ErrorHandler;
  let notice: FailureNotice;
  let sent: ErrorReport[];
  let sinkDown: boolean;

  beforeEach(() => {
    sent = [];
    sinkDown = false;
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [
        provideHarmonyErrors(),
        {
          provide: ERROR_REPORT_SINK,
          useValue: {
            send: (report: ErrorReport) => {
              if (sinkDown) throw new Error('sink down');
              sent.push(report);
            },
          },
        },
        {
          provide: RUNTIME_CONFIG,
          useValue: { environment: 'test', release: 'r1', minimumClientVersion: '0' },
        },
      ],
    });
    handler = TestBed.inject(ErrorHandler);
    notice = TestBed.inject(FailureNotice);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('reports once, scrubbed, and shows the person a reference', () => {
    handler.handleError(new TypeError('cannot read guest Ahmad Saleh'));
    expect(sent).toHaveLength(1);
    expect(sent[0]).toMatchObject({ kind: 'unexpected', code: 'TypeError', release: 'r1' });
    expect(JSON.stringify(sent[0])).not.toContain('Ahmad');
    expect(sent[0]?.reference).toHaveLength(REFERENCE_LENGTH);
    expect(notice.current()?.reference).toBe(sent[0]?.reference);
    expect(notice.current()?.message).not.toBe('');
    expect(console.error).toHaveBeenCalledWith(expect.any(TypeError));
  });

  it('the same error again is one report, not a storm', () => {
    handler.handleError(new Error('same'));
    handler.handleError(new Error('same'));
    expect(sent).toHaveLength(1);
  });

  it('stops reporting after the cap; the notice still shows', () => {
    Array.from({ length: BEYOND_THE_CAP }, (_, i) => new Error(`e${String(i)}`)).forEach(
      (error) => {
        handler.handleError(error);
      },
    );
    expect(sent).toHaveLength(REPORT_CAP);
    expect(notice.current()).not.toBeNull();
  });

  it('a sink that fails never throws out of the handler', () => {
    sinkDown = true;
    expect(() => {
      handler.handleError(new Error('x'));
    }).not.toThrow();
  });

  it('a thrown non-Error is reported by its type', () => {
    handler.handleError('boom');
    expect(sent[0]?.code).toBe('string');
  });
});
