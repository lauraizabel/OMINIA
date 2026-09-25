import { expect, test } from './support/fixtures';
import { expiredToken } from './support/jwt';
import { fillSale, uniqueSale } from './support/sales';
import { signIn } from './support/session';

test('E12 protects a direct route, reloads safely and restores it after each login', async ({
  page,
}) => {
  await page.goto('/sales/new?returnUrl=%2Fsales');
  await expect(page).toHaveURL(/\/login\?returnUrl=/);
  await signIn(page, '/sales/new?returnUrl=%2Fsales');
  await expect(page.getByRole('heading', { name: 'Create sale' })).toBeVisible();
  await page.reload();
  await expect(page).toHaveURL(/\/login\?returnUrl=/);
  await signIn(page, '/sales/new?returnUrl=%2Fsales');
  await expect(page.getByRole('heading', { name: 'Create sale' })).toBeVisible();
});

test('E09 uses a genuinely expired token and neither loops nor resends a write', async ({
  authenticationResponse,
  page,
}) => {
  const expiredAuthentication = {
    ...authenticationResponse,
    data: {
      ...authenticationResponse.data,
      token: expiredToken(authenticationResponse.data.token),
    },
  };
  await page.unroute('**/api/auth');
  await page.route('**/api/auth', async (route) => {
    await route.fulfill({ status: 200, json: expiredAuthentication });
  });
  await signIn(page, '/sales/new?returnUrl=%2Fsales');
  await fillSale(page, uniqueSale('E2E-EXPIRED'));
  let writes = 0;
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().endsWith('/api/sales')) writes += 1;
  });
  await page.getByRole('button', { name: 'Create sale' }).click();
  await expect(page).toHaveURL(/\/login\?returnUrl=/);
  await expect(page.getByRole('heading', { name: 'Sign in to continue' })).toBeVisible();
  await page.waitForTimeout(500);
  expect(writes).toBe(1);
});
