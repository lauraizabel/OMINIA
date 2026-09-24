import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api-base-url';
import { AuthSession } from './auth.models';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  const session: AuthSession = {
    token: 'signed-token',
    email: 'manager@example.com',
    name: 'Sales Manager',
    role: 'Manager',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts without a persisted session', () => {
    expect(auth.session()).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('stores the authenticated session only after a successful login', () => {
    let result: AuthSession | undefined;
    auth.login({ email: session.email, password: 'secret' }).subscribe((value) => (result = value));

    const request = http.expectOne('/api/auth');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: session.email, password: 'secret' });
    request.flush({ success: true, message: 'Authenticated', data: session });

    expect(result).toEqual(session);
    expect(auth.session()).toEqual(session);
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('clears the session on logout and reports expiration only once', () => {
    auth.login({ email: session.email, password: 'secret' }).subscribe();
    http.expectOne('/api/auth').flush({ success: true, message: 'Authenticated', data: session });

    expect(auth.expireSession()).toBe(true);
    expect(auth.expireSession()).toBe(false);
    expect(auth.session()).toBeNull();

    auth.logout();
    expect(auth.isAuthenticated()).toBe(false);
  });
});
