import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { Observable, of, Subject, throwError } from 'rxjs';
import { SalesApiService } from '../data-access/sales-api.service';
import {
  CreateSaleRequest,
  Sale,
  SaleResource,
  UpdateSaleRequest,
} from '../data-access/sales.models';
import { SaleEditor } from './sale-editor';

describe('SaleEditor create mode', () => {
  let fixture: ComponentFixture<SaleEditor>;
  let component: SaleEditor;
  const createResult = new Subject<SaleResource>();
  const create = vi.fn<(request: CreateSaleRequest) => Observable<SaleResource>>(() =>
    createResult.asObservable(),
  );

  beforeEach(async () => {
    create.mockClear();
    await TestBed.configureTestingModule({
      imports: [SaleEditor],
      providers: [
        provideRouter([]),
        { provide: SalesApiService, useValue: { create } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: () => null }, queryParamMap: { get: () => null } },
          },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(SaleEditor);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('builds a write payload without calculated or server-owned fields', () => {
    component.form.patchValue({
      saleNumber: ' sale-1 ',
      saleDate: '2026-09-24T10:00',
      customerExternalId: ' C-1 ',
      customerName: ' Customer ',
      branchExternalId: ' B-1 ',
      branchName: ' Branch ',
    });
    component.items.at(0).patchValue({
      productExternalId: ' P-1 ',
      productName: ' Product ',
      quantity: 4,
      unitPrice: 10,
    });
    component.submit();

    expect(create).toHaveBeenCalledOnce();
    const payload = create.mock.calls[0][0] as CreateSaleRequest;
    expect(payload.saleNumber).toBe('sale-1');
    expect(payload.items[0]).toEqual({
      product: { externalId: 'P-1', name: 'Product' },
      quantity: 4,
      unitPrice: 10,
    });
    expect(Object.keys(payload.items[0]).sort()).toEqual(['product', 'quantity', 'unitPrice']);
  });

  it('blocks duplicate product IDs before sending', () => {
    component.form.patchValue({
      saleNumber: 'SALE-1',
      saleDate: '2026-09-24T10:00',
      customerExternalId: 'C-1',
      customerName: 'Customer',
      branchExternalId: 'B-1',
      branchName: 'Branch',
    });
    component.items.at(0).patchValue({ productExternalId: 'P-1', productName: 'One' });
    component.addItem();
    component.items.at(1).patchValue({ productExternalId: 'P-1', productName: 'Two' });
    component.submit();

    expect(component.items.hasError('duplicateProduct')).toBe(true);
    expect(create).not.toHaveBeenCalled();
  });

  it('prevents a second submission while the first request is pending', () => {
    component.form.patchValue({
      saleNumber: 'SALE-1',
      saleDate: '2026-09-24T10:00',
      customerExternalId: 'C-1',
      customerName: 'Customer',
      branchExternalId: 'B-1',
      branchName: 'Branch',
    });
    component.items.at(0).patchValue({ productExternalId: 'P-1', productName: 'Product' });
    component.submit();
    component.submit();
    expect(create).toHaveBeenCalledOnce();
  });

  it('reports validation causes, protects item bounds, and tracks pending changes', () => {
    expect(component.hasPendingChanges()).toBe(true);
    component.form.markAsPristine();
    expect(component.hasPendingChanges()).toBe(false);

    const saleNumber = component.form.controls.saleNumber;
    saleNumber.markAsTouched();
    saleNumber.setValue('');
    expect(component.errorFor(saleNumber, 'saleNumber')).toBe('This field is required.');
    saleNumber.setValue(' '.repeat(2));
    expect(component.errorFor(saleNumber, 'saleNumber')).toBe(
      'This field cannot contain only spaces.',
    );
    saleNumber.setValue('x'.repeat(51));
    expect(component.errorFor(saleNumber, 'saleNumber')).toBe('Maximum length is 50.');

    const item = component.items.at(0);
    item.controls.quantity.markAsTouched();
    item.controls.quantity.setValue(0);
    expect(component.errorFor(item.controls.quantity, 'items[0].quantity')).toBe(
      'Minimum value is 1.',
    );
    item.controls.quantity.setValue(21);
    expect(component.errorFor(item.controls.quantity, 'items[0].quantity')).toBe(
      'Maximum value is 20.',
    );
    item.controls.unitPrice.markAsTouched();
    item.controls.unitPrice.setValue(1.234);
    expect(component.errorFor(item.controls.unitPrice, 'items[0].unitPrice')).toContain(
      'positive amount',
    );
    item.controls.productExternalId.markAsTouched();
    item.controls.productExternalId.setValue('bad\u0001id');
    expect(component.errorFor(item.controls.productExternalId, 'items[0].product.externalId')).toBe(
      'Control characters are not allowed.',
    );

    component.addItem();
    component.removeItem(1);
    component.removeItem(0);
    expect(component.items.length).toBe(1);
  });

  it('maps server field errors, clears them on submit, and returns from create mode', () => {
    const router = TestBed.inject(Router);
    const navigation = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    component.serverFields.set({ SaleNumber: 'Already used.' });
    expect(component.errorFor(component.form.controls.saleNumber, 'saleNumber')).toBe(
      'Already used.',
    );
    component.cancel();
    expect(navigation).toHaveBeenCalledWith('/sales');

    component.submit();
    expect(component.submitError()).toContain('highlighted fields');
    expect(component.serverFields()).toEqual({});
  });

  it('navigates to the new resource and marks the form saved after success', () => {
    const router = TestBed.inject(Router);
    const navigation = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const toast = TestBed.inject(ToastService);
    const success = vi.spyOn(toast, 'success');
    fillValidForm(component);
    component.submit();
    createResult.next(saleResource());

    expect(success).toHaveBeenCalledWith('Sale created successfully.');
    expect(navigation).toHaveBeenCalledWith(
      ['/sales', 'sale-id'],
      expect.objectContaining({ queryParams: { returnUrl: '/sales' } }),
    );
    expect(component.hasPendingChanges()).toBe(false);
  });

  it('presents API field errors without losing the draft', () => {
    create.mockReturnValueOnce(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            statusText: 'Bad Request',
            error: {
              detail: 'Correct the fields.',
              errors: [{ field: 'Customer.Name', code: 'Invalid', message: 'Invalid customer.' }],
            },
          }),
      ),
    );
    fillValidForm(component);
    component.submit();
    expect(component.submitError()).toBe('Correct the fields.');
    expect(component.errorFor(component.form.controls.customerName, 'customer.name')).toBe(
      'Invalid customer.',
    );
    expect(component.saving()).toBe(false);
  });
});

describe('SaleEditor edit mode', () => {
  it('preserves the draft after a stale ETag response', async () => {
    const resource = saleResource();
    const update = vi.fn<
      (id: string, request: UpdateSaleRequest, etag: string) => Observable<SaleResource>
    >(() =>
      throwError(
        () =>
          new HttpErrorResponse({
            status: 412,
            statusText: 'Precondition Failed',
            error: { type: 'ConcurrencyError', detail: 'Stale version.' },
          }),
      ),
    );
    await TestBed.configureTestingModule({
      imports: [SaleEditor],
      providers: [
        provideRouter([]),
        { provide: SalesApiService, useValue: { get: () => of(resource), update } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: { get: () => 'sale-id' },
              queryParamMap: { get: () => '/sales?_page=2' },
            },
          },
        },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(SaleEditor);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    component.form.controls.customerName.setValue('Locally edited customer');
    component.submit();

    expect(component.conflict()).toBe(true);
    expect(component.form.controls.customerName.value).toBe('Locally edited customer');
    expect(update.mock.calls[0][2]).toBe('"v7"');
  });

  it('loads active and cancelled items, navigates back, and reloads only with confirmation', async () => {
    const resource = saleResource();
    const cancelled = { ...resource.sale.items[0], id: 'cancelled-id', isCancelled: true };
    resource.sale.items.push(cancelled);
    const get = vi.fn(() => of(resource));
    const { component, router } = await createEditComponent({ get, update: vi.fn() });

    expect(component.items.length).toBe(1);
    expect(component.cancelledItems()).toHaveLength(1);
    expect(component.saleNumber()).toBe('SALE-1');
    expect(component.form.pristine).toBe(true);
    component.cancel();
    expect(router.navigate).toHaveBeenCalledWith(
      ['/sales', 'sale-id'],
      expect.objectContaining({ queryParams: { returnUrl: '/sales?_page=2' } }),
    );

    component.form.markAsDirty();
    vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValueOnce(true);
    component.reloadCurrent();
    expect(get).toHaveBeenCalledTimes(1);
    component.reloadCurrent();
    expect(get).toHaveBeenCalledTimes(2);
  });

  it('shows load failures and retries a clean edit form without confirmation', async () => {
    const get = vi
      .fn()
      .mockReturnValueOnce(
        throwError(() => new HttpErrorResponse({ status: 503, statusText: 'Unavailable' })),
      )
      .mockReturnValueOnce(of(saleResource()));
    const { component } = await createEditComponent({ get, update: vi.fn() });
    expect(component.loadError()).toContain('required service');
    component.reloadCurrent();
    expect(get).toHaveBeenCalledTimes(2);
    expect(component.loading()).toBe(false);
  });
});

function fillValidForm(component: SaleEditor): void {
  component.form.patchValue({
    saleNumber: 'SALE-1',
    saleDate: '2026-09-24T10:00',
    customerExternalId: 'C-1',
    customerName: 'Customer',
    branchExternalId: 'B-1',
    branchName: 'Branch',
  });
  component.items.at(0).patchValue({
    productExternalId: 'P-1',
    productName: 'Product',
    quantity: 4,
    unitPrice: 10,
  });
}

async function createEditComponent(api: {
  get: ReturnType<typeof vi.fn>;
  update: ReturnType<typeof vi.fn>;
}): Promise<{ component: SaleEditor; router: { navigate: ReturnType<typeof vi.fn> } }> {
  const router = { navigate: vi.fn().mockResolvedValue(true), navigateByUrl: vi.fn() };
  await TestBed.configureTestingModule({
    imports: [SaleEditor],
    providers: [
      { provide: Router, useValue: router },
      { provide: SalesApiService, useValue: api },
      {
        provide: ActivatedRoute,
        useValue: {
          snapshot: {
            paramMap: { get: () => 'sale-id' },
            queryParamMap: { get: () => '/sales?_page=2' },
          },
        },
      },
    ],
  }).compileComponents();
  const fixture = TestBed.createComponent(SaleEditor);
  fixture.detectChanges();
  return { component: fixture.componentInstance, router };
}

function saleResource(): SaleResource {
  const sale: Sale = {
    id: 'sale-id',
    saleNumber: 'SALE-1',
    saleDate: '2026-09-24T10:00:00Z',
    customer: { externalId: 'C-1', name: 'Customer' },
    branch: { externalId: 'B-1', name: 'Branch' },
    items: [
      {
        id: 'item-id',
        product: { externalId: 'P-1', name: 'Product' },
        quantity: 4,
        unitPrice: 10,
        discountRate: 0.1,
        grossAmount: 40,
        discountAmount: 4,
        totalAmount: 36,
        effectiveAmount: 36,
        isCancelled: false,
        cancelledAt: null,
      },
    ],
    totalAmount: 36,
    isCancelled: false,
    cancelledAt: null,
    createdAt: '2026-09-24T10:00:01Z',
    updatedAt: '2026-09-24T10:00:01Z',
    version: 7,
  };
  return { sale, etag: '"v7"' };
}
