import { DOCUMENT } from '@angular/common';
import { HttpErrorResponse, HttpEvent, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize, Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../config/api-base-url';
import { BrowserTelemetry } from './browser-telemetry';

export const telemetryInterceptor: HttpInterceptorFn = (request, next): Observable<HttpEvent<unknown>> => {
  const apiBaseUrl = inject(API_BASE_URL);
  const document = inject(DOCUMENT);
  if (!targetsApi(request.url, apiBaseUrl, document.baseURI)) return next(request);

  const telemetry = inject(BrowserTelemetry);
  const requestTrace = telemetry.startRequest(request.method, request.urlWithParams);
  const tracedRequest = requestTrace.traceparent
    ? request.clone({ setHeaders: { traceparent: requestTrace.traceparent } })
    : request;

  return next(tracedRequest).pipe(
    tap({
      next: (event) => {
        if (event instanceof HttpResponse) requestTrace.complete(event.status);
      },
      error: (error: unknown) =>
        requestTrace.fail(error instanceof HttpErrorResponse ? error.status : undefined),
    }),
    finalize(() => requestTrace.end()),
  );
};

function targetsApi(requestUrl: string, apiBaseUrl: string, documentBaseUrl: string): boolean {
  const request = new URL(requestUrl, documentBaseUrl);
  const api = new URL(apiBaseUrl, documentBaseUrl);
  const apiPath = api.pathname.replace(/\/$/, '');
  return (
    request.origin === api.origin &&
    (request.pathname === apiPath || request.pathname.startsWith(`${apiPath}/`))
  );
}
