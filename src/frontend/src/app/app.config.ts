import {
  ApplicationConfig,
  ErrorHandler,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './core/auth/auth.interceptor';
import { API_BASE_URL } from './core/config/api-base-url';
import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';
import { telemetryInterceptor } from './core/observability/telemetry.interceptor';
import { TelemetryErrorHandler } from './core/observability/telemetry-error-handler';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([telemetryInterceptor, authInterceptor])),
    { provide: ErrorHandler, useClass: TelemetryErrorHandler },
    provideAppInitializer(() => inject(AuthService).restoreSession()),
    { provide: API_BASE_URL, useValue: '/api' },
  ],
};
