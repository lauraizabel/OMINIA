import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
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
});

describe('SaleEditor edit mode', () => {
  it('preserves the draft after a stale ETag response', async () => {
    const resource = saleResource();
    const update = vi.fn((_id: string, _request: UpdateSaleRequest, _etag: string) =>
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
});

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
