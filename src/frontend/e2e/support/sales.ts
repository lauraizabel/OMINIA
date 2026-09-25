import { APIRequestContext, expect, Page } from '@playwright/test';

export interface SaleDraft {
  saleNumber: string;
  customerId: string;
  customerName: string;
  branchId: string;
  branchName: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
}

export function uniqueSale(prefix: string): SaleDraft {
  const suffix =
    `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`.toUpperCase();
  return {
    saleNumber: `${prefix}-${suffix}`,
    customerId: `CUSTOMER-${suffix}`,
    customerName: 'E2E Customer',
    branchId: `BRANCH-${suffix}`,
    branchName: 'E2E Branch',
    productId: `PRODUCT-${suffix}`,
    productName: 'E2E Product',
    quantity: 4,
    unitPrice: 10,
  };
}

export async function fillSale(page: Page, sale: SaleDraft): Promise<void> {
  await page.getByLabel('Sale number').fill(sale.saleNumber);

  const customer = page.getByRole('region', { name: 'Customer' });
  await customer.getByLabel('External ID').fill(sale.customerId);
  await customer.getByLabel('Name').fill(sale.customerName);

  const branch = page.getByRole('region', { name: 'Branch' });
  await branch.getByLabel('External ID').fill(sale.branchId);
  await branch.getByLabel('Name').fill(sale.branchName);

  const item = page.locator('[data-item-index="0"]');
  await item.getByLabel('Product ID').fill(sale.productId);
  await item.getByLabel('Product name').fill(sale.productName);
  await item.getByLabel('Quantity').fill(String(sale.quantity));
  await item.getByLabel('Unit price').fill(String(sale.unitPrice));

  // Confirm that the reactive form observed the initial field before submission.
  await expect(page.getByLabel('Sale number')).toHaveValue(sale.saleNumber);
}

export async function createSale(page: Page, sale: SaleDraft): Promise<string> {
  await page.getByRole('link', { name: 'New sale' }).click();
  await expect(page).toHaveURL(/\/sales\/new/);
  await fillSale(page, sale);
  await page.getByRole('button', { name: 'Create sale' }).click();
  await expect(page.getByRole('heading', { name: sale.saleNumber })).toBeVisible();
  const match = page.url().match(/\/sales\/([0-9a-f-]{36})/i);
  if (!match) throw new Error(`Created sale URL did not contain an id: ${page.url()}`);
  return match[1];
}

export async function findSale(page: Page, saleNumber: string): Promise<void> {
  await page.getByRole('button', { name: 'Back to sales' }).click();
  await page.getByPlaceholder('Search by sale number…').fill(saleNumber);
  await page.getByRole('button', { name: 'Apply' }).click();
  await expect(page.getByText(saleNumber, { exact: true })).toBeVisible();
}

export function salePayload(sale: SaleDraft, quantity = sale.quantity) {
  return {
    saleNumber: sale.saleNumber,
    saleDate: new Date().toISOString(),
    customer: { externalId: sale.customerId, name: sale.customerName },
    branch: { externalId: sale.branchId, name: sale.branchName },
    items: [
      {
        product: { externalId: sale.productId, name: sale.productName },
        quantity,
        unitPrice: sale.unitPrice,
      },
    ],
  };
}

export async function createSaleThroughApi(
  request: APIRequestContext,
  token: string,
  sale: SaleDraft,
  quantity = sale.quantity,
) {
  const response = await request.post('/api/sales', {
    headers: { Authorization: `Bearer ${token}` },
    data: salePayload(sale, quantity),
  });
  expect(response.status()).toBe(201);
  return { response, body: await response.json(), etag: response.headers()['etag'] };
}
