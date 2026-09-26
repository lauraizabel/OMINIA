import { ErrorHandler, inject, Injectable } from '@angular/core';
import { BrowserTelemetry } from './browser-telemetry';

@Injectable()
export class TelemetryErrorHandler implements ErrorHandler {
  private readonly telemetry = inject(BrowserTelemetry);

  handleError(error: unknown): void {
    this.telemetry.recordUnhandledError(error);
    console.error(error);
  }
}
