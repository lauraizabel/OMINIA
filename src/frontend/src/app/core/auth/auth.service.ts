import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiEnvelope } from '../api/api.models';
import { API_BASE_URL } from '../config/api-base-url';
import { AuthSession, LoginRequest } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);
  private readonly currentSession = signal<AuthSession | null>(null);

  readonly session = this.currentSession.asReadonly();
  readonly isAuthenticated = computed(() => this.currentSession() !== null);

  login(credentials: LoginRequest): Observable<AuthSession> {
    return this.http.post<ApiEnvelope<AuthSession>>(`${this.apiBaseUrl}/auth`, credentials).pipe(
      map((response) => {
        this.currentSession.set(response.data);
        return response.data;
      }),
    );
  }

  logout(): void {
    this.currentSession.set(null);
  }

  expireSession(): boolean {
    const hadSession = this.currentSession() !== null;
    this.currentSession.set(null);
    return hadSession;
  }
}
