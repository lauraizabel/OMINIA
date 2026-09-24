import { DOCUMENT } from '@angular/common';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../config/api-base-url';
import { AuthService } from './auth.service';
import { safeReturnUrl } from './return-url';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const document = inject(DOCUMENT);
  const apiBaseUrl = inject(API_BASE_URL);
  const targetsApi = isApiRequest(request.url, apiBaseUrl, document.baseURI);
  const token = auth.session()?.token;
  const authenticatedRequest =
    targetsApi && token
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

  return next(authenticatedRequest).pipe(
    catchError((error: unknown) => {
      if (targetsApi && error instanceof HttpErrorResponse) {
        if (error.status === 401 && auth.expireSession()) {
          void router.navigate(['/login'], {
            queryParams: { returnUrl: safeReturnUrl(router.url) },
          });
        } else if (error.status === 403 && auth.isAuthenticated()) {
          void router.navigate(['/forbidden']);
        }
      }

      return throwError(() => error);
    }),
  );
};

function isApiRequest(requestUrl: string, apiBaseUrl: string, documentBaseUrl: string): boolean {
  const request = new URL(requestUrl, documentBaseUrl);
  const api = new URL(apiBaseUrl, documentBaseUrl);
  const normalizedApiPath = api.pathname.replace(/\/$/, '');

  return (
    request.origin === api.origin &&
    (request.pathname === normalizedApiPath || request.pathname.startsWith(`${normalizedApiPath}/`))
  );
}
