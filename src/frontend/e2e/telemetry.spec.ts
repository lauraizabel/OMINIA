import { expect, test } from './support/fixtures';
import { signIn, testCredentials } from './support/session';

test('E13 exports sanitized browser telemetry and propagates W3C trace context', async ({ page }) => {
  const exports: string[] = [];
  const traceparents: string[] = [];

  await page.route('**/telemetry-config.json', (route) =>
    route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        enabled: true,
        serviceName: 'ambev-sales-portal-e2e',
        endpoint: '/otel/v1/traces',
      }),
    }),
  );
  await page.route('**/otel/v1/traces', async (route) => {
    exports.push(route.request().postData() ?? '');
    await route.fulfill({ status: 200, body: '{}' });
  });
  page.on('request', (request) => {
    if (new URL(request.url()).pathname.startsWith('/api/')) {
      const traceparent = request.headers()['traceparent'];
      if (traceparent) traceparents.push(traceparent);
    }
  });

  await signIn(page);
  await expect.poll(() => exports.length, { timeout: 5_000 }).toBeGreaterThan(0);

  expect(traceparents).not.toHaveLength(0);
  expect(traceparents.every((value) => /^00-[0-9a-f]{32}-[0-9a-f]{16}-0[01]$/.test(value))).toBe(
    true,
  );

  const exported = exports.join('');
  const credentials = testCredentials();
  expect(exported).not.toContain(credentials.email);
  expect(exported).not.toContain(credentials.password);
  expect(exported).not.toContain('Authorization');
});
