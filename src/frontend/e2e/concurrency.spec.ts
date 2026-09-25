import { expect, test } from './support/fixtures';
import { createSale, uniqueSale } from './support/sales';
import { signIn } from './support/session';

test('E07 preserves the stale draft when two sessions update the same ETag', async ({
  authenticationResponse,
  browser,
}) => {
  const firstContext = await browser.newContext();
  const secondContext = await browser.newContext();
  for (const context of [firstContext, secondContext]) {
    await context.route('**/api/auth', async (route) => {
      await route.fulfill({ status: 200, json: authenticationResponse });
    });
  }
  const first = await firstContext.newPage();
  const second = await secondContext.newPage();
  const sale = uniqueSale('E2E-CONFLICT');

  await signIn(first);
  const id = await createSale(first, sale);
  await signIn(second, `/sales/${id}/edit?returnUrl=%2Fsales`);
  await expect(second.getByLabel('Sale number')).toHaveValue(sale.saleNumber);
  await first.getByRole('link', { name: 'Edit sale' }).click();
  await expect(first.getByLabel('Sale number')).toHaveValue(sale.saleNumber);

  await first.getByRole('region', { name: 'Customer' }).getByLabel('Name').fill('First writer');
  const firstUpdate = first.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' && response.url().includes(`/api/sales/${id}`),
  );
  await first.getByRole('button', { name: 'Save changes' }).click();
  expect((await firstUpdate).status()).toBe(200);
  await expect(first.getByText('First writer')).toBeVisible();

  const staleName = second.getByRole('region', { name: 'Customer' }).getByLabel('Name');
  await staleName.fill('Second writer draft');
  const staleUpdate = second.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' && response.url().includes(`/api/sales/${id}`),
  );
  await second.getByRole('button', { name: 'Save changes' }).click();
  expect((await staleUpdate).status()).toBe(412);
  await expect(second.getByText(/changed after you opened it/i)).toBeVisible();
  await expect(staleName).toHaveValue('Second writer draft');

  await firstContext.close();
  await secondContext.close();
});
