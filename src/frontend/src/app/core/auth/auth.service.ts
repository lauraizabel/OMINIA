import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, firstValueFrom, map, Observable, of, shareReplay } from 'rxjs';
import { ApiEnvelope } from '../api/api.models';
import { API_BASE_URL } from '../config/api-base-url';
import { AuthSession, LoginRequest } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);
  private readonly currentSession = signal<AuthSession | null>(null);
  private refreshRequest?: Observable<AuthSession>;

  readonly session = this.currentSession.asReadonly();
  readonly isAuthenticated = computed(() => this.currentSession() !== null);

  login(credentials: LoginRequest): Observable<AuthSession> {
    return this.http
      .post<ApiEnvelope<AuthSession>>(`${this.apiBaseUrl}/auth`, credentials, {
        withCredentials: true,
      })
      .pipe(
        map((response) => {
          this.currentSession.set(response.data);
          return response.data;
        }),
      );
  }

  refreshSession(): Observable<AuthSession> {
    if (this.refreshRequest) {
      return this.refreshRequest;
    }

    this.refreshRequest = this.http
      .post<ApiEnvelope<AuthSession>>(`${this.apiBaseUrl}/auth/refresh`, null, {
        withCredentials: true,
      })
      .pipe(
        map((response) => {
          this.currentSession.set(response.data);
          return response.data;
        }),
        finalize(() => (this.refreshRequest = undefined)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.refreshRequest;
  }

  async restoreSession(): Promise<void> {
    await firstValueFrom(
      this.refreshSession().pipe(
        map(() => undefined),
        catchError(() => {
          this.currentSession.set(null);
          return of(undefined);
        }),
      ),
    );
  }

  logout(): Observable<void> {
    this.currentSession.set(null);
    return this.http
      .post<void>(`${this.apiBaseUrl}/auth/logout`, null, { withCredentials: true })
      .pipe(catchError(() => of(undefined)));
  }

  expireSession(): boolean {
    const hadSession = this.currentSession() !== null;
    this.currentSession.set(null);
    return hadSession;
  }
}
