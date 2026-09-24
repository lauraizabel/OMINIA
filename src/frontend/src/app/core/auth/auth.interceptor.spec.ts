import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../config/api-base-url';
import { authInterceptor } from './auth.interceptor';
import { AuthSession } from './auth.models';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  const navigate = vi.fn().mockResolvedValue(true);
  const router = { navigate, url: '/sales/42' };
  const session: AuthSession = {
    token: 'signed-token',
    email: 'manager@example.com',
    name: 'Sales Manager',
    role: 'Manager',
  };

  beforeEach(() => {
    navigate.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
        { provide: Router, useValue: router },
      ],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function authenticate(): void {
    auth.login({ email: session.email, password: 'secret' }).subscribe();
    http.expectOne('/api/auth').flush({ success: true, message: 'Authenticated', data: session });
  }

  it('adds the token only to requests inside the configured API boundary', () => {
    authenticate();
    const client = TestBed.inject(HttpClient);

    client.get('/api/sales').subscribe();
    expect(http.expectOne('/api/sales').request.headers.get('Authorization')).toBe(
      'Bearer signed-token',
    );

    client.get('https://telemetry.example/events').subscribe();
    expect(
      http.expectOne('https://telemetry.example/events').request.headers.has('Authorization'),
    ).toBe(false);
  });

  it('clears an expired session and redirects once without retrying the request', async () => {
    authenticate();
    const client = TestBed.inject(HttpClient);

    const firstCall = firstValueFrom(client.get('/api/sales')).catch(() => undefined);
    http.expectOne('/api/sales').flush({}, { status: 401, statusText: 'Unauthorized' });
    await firstCall;

    expect(auth.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/sales/42' } });

    const secondCall = firstValueFrom(client.get('/api/sales')).catch(() => undefined);
    http.expectOne('/api/sales').flush({}, { status: 401, statusText: 'Unauthorized' });
    await secondCall;

    expect(navigate).toHaveBeenCalledOnce();
  });

  it('keeps the session and routes forbidden responses to the access-denied page', async () => {
    authenticate();
    const client = TestBed.inject(HttpClient);

    const call = firstValueFrom(client.get('/api/sales')).catch(() => undefined);
    http.expectOne('/api/sales').flush({}, { status: 403, statusText: 'Forbidden' });
    await call;

    expect(auth.isAuthenticated()).toBe(true);
    expect(navigate).toHaveBeenCalledWith(['/forbidden']);
  });
});
