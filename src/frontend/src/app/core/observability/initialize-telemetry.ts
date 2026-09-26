import { BatchSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME } from '@opentelemetry/semantic-conventions';

interface BrowserTelemetryConfig {
  enabled: boolean;
  serviceName: string;
  endpoint: string;
}

let provider: WebTracerProvider | undefined;

export async function initializeTelemetry(): Promise<void> {
  const response = await fetch('/telemetry-config.json', {
    cache: 'no-store',
    credentials: 'same-origin',
  });
  if (!response.ok) return;

  const config: unknown = await response.json();
  if (!isTelemetryConfig(config) || !config.enabled || provider) return;

  const endpoint = new URL(config.endpoint, document.baseURI);
  if (endpoint.origin !== window.location.origin) return;

  provider = new WebTracerProvider({
    resource: resourceFromAttributes({ [ATTR_SERVICE_NAME]: config.serviceName }),
    spanProcessors: [
      new BatchSpanProcessor(new OTLPTraceExporter({ url: endpoint.toString() }), {
        scheduledDelayMillis: 1_000,
        maxQueueSize: 256,
        maxExportBatchSize: 64,
      }),
    ],
  });
  provider.register();
}

function isTelemetryConfig(value: unknown): value is BrowserTelemetryConfig {
  if (!value || typeof value !== 'object') return false;
  const candidate = value as Partial<BrowserTelemetryConfig>;
  return (
    typeof candidate.enabled === 'boolean' &&
    typeof candidate.serviceName === 'string' &&
    candidate.serviceName.trim().length > 0 &&
    typeof candidate.endpoint === 'string' &&
    candidate.endpoint.trim().length > 0
  );
}
