import { DOCUMENT } from '@angular/common';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { API_BASE_URL } from '../config/api-base-url';
import { AuthService } from './auth.service';
import { safeReturnUrl } from './return-url';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const document = inject(DOCUMENT);
  const apiBaseUrl = inject(API_BASE_URL);
  const targetsApi = isApiRequest(request.url, apiBaseUrl, document.baseURI);
  const sessionEndpoint = isSessionEndpoint(request.url, apiBaseUrl, document.baseURI);
  const token = auth.session()?.token;
  const authenticatedRequest =
    targetsApi && token
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

  return next(authenticatedRequest).pipe(
    catchError((error: unknown) => {
      if (targetsApi && error instanceof HttpErrorResponse) {
        if (error.status === 401 && token && !sessionEndpoint) {
          return auth.refreshSession().pipe(
            switchMap((session) =>
              next(
                request.clone({
                  setHeaders: { Authorization: `Bearer ${session.token}` },
                }),
              ),
            ),
            catchError(() => {
              redirectAfterExpiration(auth, router);
              return throwError(() => error);
            }),
          );
        }

        if (error.status === 401 && !sessionEndpoint) {
          redirectAfterExpiration(auth, router);
        } else if (error.status === 403 && auth.isAuthenticated()) {
          void router.navigate(['/forbidden']);
        }
      }

      return throwError(() => error);
    }),
  );
};

function redirectAfterExpiration(auth: AuthService, router: Router): void {
  if (auth.expireSession()) {
    void router.navigate(['/login'], {
      queryParams: { returnUrl: safeReturnUrl(router.url) },
    });
  }
}

function isApiRequest(requestUrl: string, apiBaseUrl: string, documentBaseUrl: string): boolean {
  const request = new URL(requestUrl, documentBaseUrl);
  const api = new URL(apiBaseUrl, documentBaseUrl);
  const normalizedApiPath = api.pathname.replace(/\/$/, '');

  return (
    request.origin === api.origin &&
    (request.pathname === normalizedApiPath || request.pathname.startsWith(`${normalizedApiPath}/`))
  );
}

function isSessionEndpoint(
  requestUrl: string,
  apiBaseUrl: string,
  documentBaseUrl: string,
): boolean {
  const request = new URL(requestUrl, documentBaseUrl);
  const api = new URL(apiBaseUrl, documentBaseUrl);
  const authPath = `${api.pathname.replace(/\/$/, '')}/auth`;
  return request.origin === api.origin && request.pathname.startsWith(authPath);
}
