import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../config/api-base-url';
import { BrowserTelemetry, ClientRequestTrace } from './browser-telemetry';
import { telemetryInterceptor } from './telemetry.interceptor';

describe('telemetryInterceptor', () => {
  const complete = vi.fn();
  const fail = vi.fn();
  const end = vi.fn();
  const requestTrace: ClientRequestTrace = {
    traceparent: '00-0123456789abcdef0123456789abcdef-0123456789abcdef-01',
    complete,
    fail,
    end,
  };
  const telemetry = { startRequest: vi.fn(() => requestTrace) };
  let client: HttpClient;
  let http: HttpTestingController;

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([telemetryInterceptor])),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
        { provide: BrowserTelemetry, useValue: telemetry },
      ],
    });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('propagates trace context to API calls and completes the span', async () => {
    const result = firstValueFrom(client.get('/api/sales?email=private@example.com'));
    const request = http.expectOne('/api/sales?email=private@example.com');
    expect(request.request.headers.get('traceparent')).toBe(requestTrace.traceparent);
    request.flush({ ok: true });
    await result;

    expect(telemetry.startRequest).toHaveBeenCalledWith(
      'GET',
      '/api/sales?email=private@example.com',
    );
    expect(complete).toHaveBeenCalledWith(200);
    expect(end).toHaveBeenCalledOnce();
  });

  it('records API failures and ignores requests outside the API boundary', async () => {
    const failed = firstValueFrom(client.get('/api/sales')).catch(() => undefined);
    http.expectOne('/api/sales').flush({}, { status: 503, statusText: 'Unavailable' });
    await failed;
    expect(fail).toHaveBeenCalledWith(503);
    expect(end).toHaveBeenCalledOnce();

    client.get('https://telemetry.example/events').subscribe();
    const external = http.expectOne('https://telemetry.example/events');
    expect(external.request.headers.has('traceparent')).toBe(false);
    external.flush({});
    expect(telemetry.startRequest).toHaveBeenCalledTimes(1);
  });
});
