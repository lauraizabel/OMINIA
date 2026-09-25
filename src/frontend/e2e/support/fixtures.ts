import { expect, request, test as base } from '@playwright/test';
import { testCredentials } from './session';

export interface AuthenticationEnvelope {
  data: {
    token: string;
    email: string;
    name: string;
    role: string;
  };
}

interface WorkerFixtures {
  authenticationResponse: AuthenticationEnvelope;
}

interface TestFixtures {
  _mockAuthentication: void;
}

export const test = base.extend<TestFixtures, WorkerFixtures>({
  authenticationResponse: [
    async ({}, use) => {
      const api = await request.newContext({
        baseURL: process.env['E2E_BASE_URL'] ?? 'http://127.0.0.1:4200',
      });
      const response = await api.post('/api/auth', { data: testCredentials() });
      expect(response.ok(), `E2E authentication failed with HTTP ${response.status()}`).toBe(true);
      await use((await response.json()) as AuthenticationEnvelope);
      await api.dispose();
    },
    { scope: 'worker' },
  ],
  _mockAuthentication: [
    async ({ page, authenticationResponse }, use) => {
      await page.route('**/api/auth', async (route) => {
        if (route.request().method() !== 'POST') return route.continue();
        await route.fulfill({ status: 200, json: authenticationResponse });
      });
      await use();
    },
    { auto: true },
  ],
});

export { expect } from '@playwright/test';
