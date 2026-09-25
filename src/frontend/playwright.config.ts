import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env['E2E_BASE_URL'] ?? 'http://127.0.0.1:4200';
const proxyConfig = process.env['E2E_PROXY_CONFIG'] ?? 'proxy.conf.json';
const frontendUrl = new URL(baseURL);

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: Boolean(process.env['CI']),
  retries: process.env['CI'] ? 2 : 0,
  failOnFlakyTests: Boolean(process.env['CI']),
  // The API deliberately rate-limits login and the tests share one configured account.
  // Keep the suite serial while individual scenarios can still open concurrent contexts.
  workers: 1,
  reporter: process.env['CI']
    ? [
        ['github'],
        ['html', { open: 'never' }],
        ['junit', { outputFile: 'artifacts/e2e/junit.xml' }],
      ]
    : 'list',
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  expect: { timeout: 10_000 },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: process.env['E2E_SKIP_WEB_SERVER']
    ? undefined
    : {
        command: `npm start -- --host ${frontendUrl.hostname} --port ${frontendUrl.port || '4200'} --proxy-config ${proxyConfig}`,
        url: baseURL,
        reuseExistingServer: !process.env['CI'],
        timeout: 120_000,
      },
});
