import { expect, Page } from '@playwright/test';

export interface TestCredentials {
  email: string;
  password: string;
}

export function testCredentials(): TestCredentials {
  const email = process.env['E2E_ADMIN_EMAIL'];
  const password = process.env['E2E_ADMIN_PASSWORD'];

  if (!email || !password) {
    throw new Error(
      'Set E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD to an active Manager or Admin account.',
    );
  }

  return { email, password };
}

export async function signIn(page: Page, returnUrl = '/sales'): Promise<void> {
  const credentials = testCredentials();
  await page.goto(`/login?returnUrl=${encodeURIComponent(returnUrl)}`);
  await page.getByLabel('Email address').fill(credentials.email);
  await page.getByLabel('Password', { exact: true }).fill(credentials.password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(new RegExp(`${escapeRegExp(returnUrl)}(?:$|\\?)`));
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
