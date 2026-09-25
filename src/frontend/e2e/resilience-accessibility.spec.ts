import AxeBuilder from '@axe-core/playwright';
import { expect, test } from './support/fixtures';
import { createSale, fillSale, findSale, uniqueSale } from './support/sales';
import { signIn } from './support/session';

test('E10 does not retry a create command after the response is interrupted', async ({
  page,
  request,
}) => {
  const sale = uniqueSale('E2E-NORETRY');
  await signIn(page);
  await page.getByRole('link', { name: 'New sale' }).click();
  await expect(page).toHaveURL(/\/sales\/new/);
  await fillSale(page, sale);

  let createRequests = 0;
  let resolvePersistedStatus!: (status: number) => void;
  let rejectPersistence!: (reason: unknown) => void;
  const persistedStatus = new Promise<number>((resolve, reject) => {
    resolvePersistedStatus = resolve;
    rejectPersistence = reject;
  });

  await page.route('**/api/sales', async (route) => {
    if (route.request().method() !== 'POST') return route.continue();
    createRequests += 1;
    const interceptedRequest = route.request();

    try {
      // Persist the command independently, then fail only the browser request. This
      // models a lost response without relying on route.fetch() abort timing.
      const persistedResponse = await request.post(interceptedRequest.url(), {
        data: interceptedRequest.postDataJSON(),
        headers: {
          authorization: interceptedRequest.headers()['authorization'],
        },
      });
      resolvePersistedStatus(persistedResponse.status());
      await route.abort('failed');
    } catch (error) {
      rejectPersistence(error);
      await route.abort('failed');
    }
  });

  await page.getByRole('button', { name: 'Create sale' }).click();
  await Promise.all([
    expect(persistedStatus).resolves.toBe(201),
    expect(page.getByText(/verify the sale before resubmitting/i)).toBeVisible(),
  ]);
  expect(createRequests).toBe(1);

  await page.unroute('**/api/sales');
  page.once('dialog', (dialog) => dialog.accept());
  await findSale(page, sale.saleNumber);
  await expect(page.getByText(sale.saleNumber, { exact: true })).toHaveCount(1);
});

test('E11 supports mobile keyboard use without serious accessibility violations', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await signIn(page);
  await page.keyboard.press('Tab');
  await expect(page.locator(':focus')).toBeVisible();

  const results = await new AxeBuilder({ page }).analyze();
  const blocking = results.violations.filter((violation) =>
    ['critical', 'serious'].includes(violation.impact ?? ''),
  );
  expect(blocking).toEqual([]);

  await page.getByRole('link', { name: 'New sale' }).click();
  await expect(page.getByLabel('Sale number')).toBeVisible();
  await expect(
    page.getByRole('region', { name: 'Customer' }).getByLabel('External ID'),
  ).toBeVisible();
  await expect(page.getByRole('region', { name: 'Branch' }).getByLabel('Name')).toBeVisible();
  await page.getByRole('button', { name: 'Create sale' }).click();
  await expect(page.getByLabel('Sale number')).toBeFocused();
});

test('E11 opens and dismisses a destructive confirmation from the keyboard', async ({ page }) => {
  const sale = uniqueSale('E2E-DIALOG');
  await signIn(page);
  await createSale(page, sale);
  const cancel = page.getByRole('button', { name: 'Cancel sale' });
  await cancel.focus();

  const dialogMessage = new Promise<string>((resolve) => {
    page.once('dialog', async (dialog) => {
      resolve(dialog.message());
      await dialog.dismiss();
    });
  });
  await page.keyboard.press('Enter');
  await expect(dialogMessage).resolves.toContain(sale.saleNumber);
  await expect(cancel).toBeFocused();
  await expect(page.getByText('Active', { exact: true }).first()).toBeVisible();
});
