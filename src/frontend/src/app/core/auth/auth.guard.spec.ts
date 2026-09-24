import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, RouterStateSnapshot } from '@angular/router';
import { API_BASE_URL } from '../config/api-base-url';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';

describe('authGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
  });

  it('redirects an anonymous deep link to login with a safe return URL', () => {
    const router = TestBed.inject(Router);
    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as never, { url: '/sales/42?_page=2' } as RouterStateSnapshot),
    );

    expect(router.serializeUrl(result as never)).toBe('/login?returnUrl=%2Fsales%2F42%3F_page%3D2');
  });

  it('allows an authenticated session', () => {
    const auth = TestBed.inject(AuthService);
    const session = {
      token: 'token',
      email: 'manager@example.com',
      name: 'Manager',
      role: 'Manager',
    };
    auth.login({ email: session.email, password: 'secret' }).subscribe();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/auth')
      .flush({ success: true, message: 'Authenticated', data: session });

    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as never, { url: '/sales' } as RouterStateSnapshot),
    );

    expect(result).toBe(true);
  });
});
