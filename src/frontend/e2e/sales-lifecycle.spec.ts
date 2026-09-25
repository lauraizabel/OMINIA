import { expect, test } from './support/fixtures';
import {
  createSale,
  createSaleThroughApi,
  fillSale,
  salePayload,
  uniqueSale,
} from './support/sales';
import { signIn } from './support/session';

test.setTimeout(60_000);

test('E01 creates four units and confirms the discount in the UI and API', async ({
  authenticationResponse,
  page,
}) => {
  const sale = uniqueSale('E2E-DISCOUNT');
  await signIn(page);
  const id = await createSale(page, sale);
  await expect(page.getByText(/R\$\s*36[,.]00/).first()).toBeVisible();

  const response = await page.request.get(`/api/sales/${id}`, {
    headers: auth(authenticationResponse.data.token),
  });
  expect(response.status()).toBe(200);
  const body = await response.json();
  expect(body.totalAmount).toBe(36);
  expect(body.items[0]).toMatchObject({
    quantity: 4,
    grossAmount: 40,
    discountRate: 0.1,
    discountAmount: 4,
    totalAmount: 36,
  });
});

test('E02 changes from three units without discount to ten units with the persisted tier', async ({
  authenticationResponse,
  page,
}) => {
  const sale = { ...uniqueSale('E2E-TIER'), quantity: 3 };
  await signIn(page);
  const id = await createSale(page, sale);
  await expect(page.getByText(/R\$\s*30[,.]00/).first()).toBeVisible();
  await page.getByRole('link', { name: 'Edit sale' }).click();
  await page.locator('[data-item-index="0"]').getByLabel('Quantity').fill('10');
  await page.getByRole('button', { name: 'Save changes' }).click();
  await expect(page.getByText(/R\$\s*80[,.]00/).first()).toBeVisible();

  const response = await page.request.get(`/api/sales/${id}`, {
    headers: auth(authenticationResponse.data.token),
  });
  expect((await response.json()).items[0]).toMatchObject({
    quantity: 10,
    discountRate: 0.2,
    totalAmount: 80,
  });
});

test('E03 rejects quantities above the limit and duplicate products through UI and API', async ({
  authenticationResponse,
  page,
}) => {
  const sale = uniqueSale('E2E-VALIDATION');
  await signIn(page);
  await page.getByRole('link', { name: 'New sale' }).click();
  await fillSale(page, { ...sale, quantity: 21 });
  const firstQuantity = page.locator('[data-item-index="0"]').getByLabel('Quantity');
  await page.getByRole('button', { name: 'Create sale' }).click();
  await expect(page.getByText('Maximum value is 20.')).toBeVisible();
  await firstQuantity.fill('4');
  await page.getByRole('button', { name: 'Add item' }).click();
  const secondItem = page.locator('[data-item-index="1"]');
  await secondItem.getByLabel('Product ID').fill(sale.productId);
  await secondItem.getByLabel('Product name').fill('Duplicate E2E Product');
  await page.getByRole('button', { name: 'Create sale' }).click();
  await expect(page.getByText('Each product external ID must be unique.')).toBeVisible();

  const headers = auth(authenticationResponse.data.token);
  const invalidQuantity = await page.request.post('/api/sales', {
    headers,
    data: salePayload({ ...uniqueSale('E2E-API-QTY'), quantity: 21 }),
  });
  expect(invalidQuantity.status()).toBe(400);
  const duplicate = uniqueSale('E2E-API-DUP');
  const duplicatePayload = salePayload(duplicate);
  duplicatePayload.items.push({ ...duplicatePayload.items[0] });
  expect(
    (await page.request.post('/api/sales', { headers, data: duplicatePayload })).status(),
  ).toBe(400);
});

test('E04 cancels one item and then the last item while retaining history', async ({
  authenticationResponse,
  page,
}) => {
  const sale = uniqueSale('E2E-ITEMS');
  await signIn(page);
  await page.getByRole('link', { name: 'New sale' }).click();
  await fillSale(page, sale);
  await page.getByRole('button', { name: 'Add item' }).click();
  const secondItem = page.locator('[data-item-index="1"]');
  await secondItem.getByLabel('Product ID').fill(`${sale.productId}-2`);
  await secondItem.getByLabel('Product name').fill('Second E2E Product');
  await secondItem.getByLabel('Quantity').fill('2');
  await secondItem.getByLabel('Unit price').fill('10');
  await page.getByRole('button', { name: 'Create sale' }).click();
  await expect(page.getByRole('heading', { name: sale.saleNumber })).toBeVisible();
  const id = page.url().match(/\/sales\/([0-9a-f-]{36})/i)?.[1] ?? '';

  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: 'Cancel item' }).first().click();
  await expect(page.getByText(/R\$\s*20[,.]00/).first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Cancel item' })).toHaveCount(1);
  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: 'Cancel item' }).click();
  await expect(page.getByText(/R\$\s*0[,.]00/).first()).toBeVisible();
  await expect(page.getByText('Cancelled', { exact: true }).first()).toBeVisible();

  const response = await page.request.get(`/api/sales/${id}`, {
    headers: auth(authenticationResponse.data.token),
  });
  const body = await response.json();
  expect(body.isCancelled).toBe(true);
  expect(body.totalAmount).toBe(0);
  expect(body.items).toHaveLength(2);
  expect(
    body.items.every(
      (item: { isCancelled: boolean; cancelledAt: string | null }) =>
        item.isCancelled && Boolean(item.cancelledAt),
    ),
  ).toBe(true);
});

test('E05 treats repeated sale cancellation as an idempotent command', async ({
  authenticationResponse,
  page,
}) => {
  const sale = uniqueSale('E2E-IDEMPOTENT');
  await signIn(page);
  const id = await createSale(page, sale);
  page.once('dialog', (dialog) => dialog.accept());
  const cancellation = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' && response.url().endsWith(`/api/sales/${id}/cancel`),
  );
  await page.getByRole('button', { name: 'Cancel sale' }).click();
  const firstResponse = await cancellation;
  expect(firstResponse.status()).toBe(200);
  const repeated = await page.request.post(`/api/sales/${id}/cancel`, {
    headers: {
      ...auth(authenticationResponse.data.token),
      'If-Match': firstResponse.headers()['etag'],
    },
  });
  expect(repeated.status()).toBe(200);
  expect((await repeated.json()).isCancelled).toBe(true);
});

test('E06 returns 404 after deletion and corrects an empty last page', async ({
  authenticationResponse,
  page,
}) => {
  const token = authenticationResponse.data.token;
  await signIn(page);
  const run = uniqueSale('E2E-PAGE');
  await Promise.all(
    Array.from({ length: 11 }, (_, index) =>
      createSaleThroughApi(page.request, token, {
        ...run,
        saleNumber: `${run.saleNumber}-${String(index + 1).padStart(2, '0')}`,
        productId: `${run.productId}-${index + 1}`,
      }),
    ),
  );
  await page.getByPlaceholder('Search by sale number…').fill(`${run.saleNumber}*`);
  await page.getByRole('button', { name: 'Apply' }).click();
  await expect(page.getByText('11 results')).toBeVisible();
  await page.getByRole('button', { name: /Next/ }).click();
  await expect(page).toHaveURL(/_page=2/);
  const onlyResult = page.getByRole('link', { name: /View sale E2E-PAGE/ });
  await expect(onlyResult).toHaveCount(1);
  const href = await onlyResult.getAttribute('href');
  const visibleId = href?.match(/\/sales\/([0-9a-f-]{36})/i)?.[1] ?? '';
  await onlyResult.click();
  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: 'Delete sale' }).click();
  await expect(page).toHaveURL(/_page=1/);
  await expect(page.getByText('10 results')).toBeVisible();
  expect(
    (await page.request.get(`/api/sales/${visibleId}`, { headers: auth(token) })).status(),
  ).toBe(404);
  const list = await page.request.get(
    `/api/sales?saleNumber=${encodeURIComponent(run.saleNumber + '*')}`,
    { headers: auth(token) },
  );
  const listBody = await list.json();
  expect(listBody.totalItems).toBe(10);
  expect(listBody.data.some((item: { id: string }) => item.id === visibleId)).toBe(false);
});

test('E08 preserves filter, order and page through detail navigation', async ({
  authenticationResponse,
  page,
}) => {
  const token = authenticationResponse.data.token;
  const run = uniqueSale('E2E-NAV');
  await signIn(page);
  await Promise.all(
    Array.from({ length: 11 }, (_, index) =>
      createSaleThroughApi(page.request, token, {
        ...run,
        saleNumber: `${run.saleNumber}-${String(index + 1).padStart(2, '0')}`,
        productId: `${run.productId}-${index + 1}`,
      }),
    ),
  );
  await page.getByPlaceholder('Search by sale number…').fill(`${run.saleNumber}*`);
  await page.getByRole('button', { name: 'More filters' }).click();
  await page.getByLabel('Order by').selectOption('saleNumber desc');
  await page.getByRole('button', { name: 'Apply' }).click();
  await page.getByRole('button', { name: /Next/ }).click();
  await expect(page).toHaveURL(/_page=2/);
  await expect(page).toHaveURL(/_order=saleNumber(?:%20|\+)desc/);
  await page.getByRole('link', { name: /View sale E2E-NAV/ }).click();
  await page.getByRole('button', { name: 'Back to sales' }).click();
  await expect(page).toHaveURL(/_page=2/);
  await expect(page).toHaveURL(/_size=10/);
  await expect(page).toHaveURL(/saleNumber=E2E-NAV/);
  await expect(page).toHaveURL(/_order=saleNumber(?:%20|\+)desc/);
});

function auth(token: string): Record<string, string> {
  return { Authorization: `Bearer ${token}` };
}
