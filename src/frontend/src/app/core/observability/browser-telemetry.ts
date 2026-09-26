import { DOCUMENT } from '@angular/common';
import { InjectionToken, inject, Injectable } from '@angular/core';
import {
  isSpanContextValid,
  Span,
  SpanKind,
  SpanStatusCode,
  trace,
  Tracer,
} from '@opentelemetry/api';

export const BROWSER_TRACER = new InjectionToken<Tracer>('BROWSER_TRACER', {
  providedIn: 'root',
  factory: () => trace.getTracer('ambev-sales-portal'),
});

export interface ClientRequestTrace {
  readonly traceparent?: string;
  complete(status: number): void;
  fail(status?: number): void;
  end(): void;
}

@Injectable({ providedIn: 'root' })
export class BrowserTelemetry {
  private readonly tracer = inject(BROWSER_TRACER);
  private readonly document = inject(DOCUMENT);

  startRequest(method: string, requestUrl: string): ClientRequestTrace {
    const route = normalizedPath(requestUrl, this.document.baseURI);
    const span = this.tracer.startSpan(`HTTP ${method.toUpperCase()} ${route}`, {
      kind: SpanKind.CLIENT,
      attributes: {
        'http.request.method': method.toUpperCase(),
        'url.path': route,
      },
    });
    const context = span.spanContext();

    return {
      traceparent: isSpanContextValid(context)
        ? `00-${context.traceId}-${context.spanId}-${context.traceFlags.toString(16).padStart(2, '0')}`
        : undefined,
      complete: (status) => complete(span, status),
      fail: (status) => fail(span, status),
      end: () => span.end(),
    };
  }

  recordUnhandledError(error: unknown): void {
    const span = this.tracer.startSpan('browser unhandled error', {
      attributes: {
        'error.type': safeErrorType(error),
        'error.source': 'angular',
      },
    });
    span.setStatus({ code: SpanStatusCode.ERROR });
    span.end();
  }
}

function complete(span: Span, status: number): void {
  span.setAttribute('http.response.status_code', status);
  if (status >= 500) span.setStatus({ code: SpanStatusCode.ERROR });
}

function fail(span: Span, status?: number): void {
  if (status !== undefined) span.setAttribute('http.response.status_code', status);
  span.setStatus({ code: SpanStatusCode.ERROR });
}

function normalizedPath(requestUrl: string, baseUrl: string): string {
  return new URL(requestUrl, baseUrl).pathname.replace(
    /\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b/gi,
    '{id}',
  );
}

function safeErrorType(error: unknown): string {
  const candidate = error instanceof Error ? error.name : typeof error;
  return candidate.replace(/[^a-z0-9_.-]/gi, '').slice(0, 80) || 'unknown';
}
