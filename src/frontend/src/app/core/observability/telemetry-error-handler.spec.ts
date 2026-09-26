import { TestBed } from '@angular/core/testing';
import { BrowserTelemetry } from './browser-telemetry';
import { TelemetryErrorHandler } from './telemetry-error-handler';

describe('TelemetryErrorHandler', () => {
  it('records the failure and preserves console diagnostics', () => {
    const telemetry = { recordUnhandledError: vi.fn() };
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [
        TelemetryErrorHandler,
        { provide: BrowserTelemetry, useValue: telemetry },
      ],
    });
    const error = new Error('private details');

    TestBed.inject(TelemetryErrorHandler).handleError(error);

    expect(telemetry.recordUnhandledError).toHaveBeenCalledWith(error);
    expect(consoleError).toHaveBeenCalledWith(error);
    consoleError.mockRestore();
  });
});
