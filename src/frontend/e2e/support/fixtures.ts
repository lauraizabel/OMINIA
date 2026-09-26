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

export const test = base.extend<Record<string, never>, WorkerFixtures>({
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
});

export { expect } from '@playwright/test';
