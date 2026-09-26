import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { Span, SpanStatusCode, Tracer } from '@opentelemetry/api';
import { BROWSER_TRACER, BrowserTelemetry } from './browser-telemetry';

describe('BrowserTelemetry', () => {
  const setAttribute = vi.fn();
  const setStatus = vi.fn();
  const end = vi.fn();
  const span = {
    spanContext: () => ({
      traceId: '0123456789abcdef0123456789abcdef',
      spanId: '0123456789abcdef',
      traceFlags: 1,
      isRemote: false,
    }),
    setAttribute,
    setStatus,
    end,
  } as unknown as Span;
  const startSpan = vi.fn(() => span);
  const tracer = { startSpan } as unknown as Tracer;

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        BrowserTelemetry,
        { provide: BROWSER_TRACER, useValue: tracer },
        { provide: DOCUMENT, useValue: { baseURI: 'https://sales.example/' } },
      ],
    });
  });

  it('creates a W3C parent and records a normalized route without query values', () => {
    const telemetry = TestBed.inject(BrowserTelemetry);
    const request = telemetry.startRequest(
      'get',
      '/api/sales/7f7dd657-24ee-4ca3-a2e6-fba29595c46b?email=private@example.com',
    );

    expect(request.traceparent).toBe(
      '00-0123456789abcdef0123456789abcdef-0123456789abcdef-01',
    );
    expect(startSpan).toHaveBeenCalledWith(
      'HTTP GET /api/sales/{id}',
      expect.objectContaining({
        attributes: {
          'http.request.method': 'GET',
          'url.path': '/api/sales/{id}',
        },
      }),
    );

    request.complete(503);
    request.end();
    expect(setAttribute).toHaveBeenCalledWith('http.response.status_code', 503);
    expect(setStatus).toHaveBeenCalledWith({ code: SpanStatusCode.ERROR });
    expect(end).toHaveBeenCalledOnce();
  });

  it('records only the error type for an unhandled browser failure', () => {
    const telemetry = TestBed.inject(BrowserTelemetry);
    telemetry.recordUnhandledError(new TypeError('private-payload-marker'));

    expect(startSpan).toHaveBeenCalledWith('browser unhandled error', {
      attributes: { 'error.type': 'TypeError', 'error.source': 'angular' },
    });
    expect(setStatus).toHaveBeenCalledWith({ code: SpanStatusCode.ERROR });
    expect(end).toHaveBeenCalledOnce();
  });
});
