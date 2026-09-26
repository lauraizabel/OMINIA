import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { SalesApiService } from '../data-access/sales-api.service';
import { SaleResource } from '../data-access/sales.models';
import { SaleDetail } from './sale-detail';

describe('SaleDetail', () => {
  let fixture: ComponentFixture<SaleDetail>;
  let component: SaleDetail;
  let api: {
    get: ReturnType<typeof vi.fn>;
    cancel: ReturnType<typeof vi.fn>;
    cancelItem: ReturnType<typeof vi.fn>;
    delete: ReturnType<typeof vi.fn>;
  };
  let router: { navigateByUrl: ReturnType<typeof vi.fn> };
  let toast: { success: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    const resource = saleResource();
    api = {
      get: vi.fn().mockReturnValue(of(resource)),
      cancel: vi
        .fn()
        .mockReturnValue(of({ ...resource, sale: { ...resource.sale, isCancelled: true } })),
      cancelItem: vi.fn().mockReturnValue(of(resource)),
      delete: vi.fn().mockReturnValue(of(undefined)),
    };
    router = { navigateByUrl: vi.fn().mockResolvedValue(true) };
    toast = { success: vi.fn() };
    await TestBed.configureTestingModule({
      imports: [SaleDetail],
      providers: [
        { provide: SalesApiService, useValue: api },
        { provide: Router, useValue: router },
        { provide: ToastService, useValue: toast },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 'sale-id' }),
              queryParamMap: convertToParamMap({ returnUrl: '/sales?_page=2' }),
            },
          },
        },
      ],
    }).compileComponents();
  });

  function create(): void {
    fixture = TestBed.createComponent(SaleDetail);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  afterEach(() => vi.restoreAllMocks());

  it('loads and renders the sale and navigates back to the safe return URL', () => {
    create();
    expect(component.loading()).toBe(false);
    expect(component.resource()?.sale.saleNumber).toBe('SALE-1');
    expect(fixture.nativeElement.textContent).toContain('Product');
    component.back();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/sales?_page=2');
  });

  it('shows a load error and can retry', () => {
    api.get
      .mockReturnValueOnce(
        throwError(() => new HttpErrorResponse({ status: 404, statusText: 'Not Found' })),
      )
      .mockReturnValueOnce(of(saleResource()));
    create();
    expect(component.error()).toContain('no longer exists');
    component.retry();
    expect(api.get).toHaveBeenCalledTimes(2);
    expect(component.resource()?.sale.id).toBe('sale-id');
  });

  it('cancels the sale and an item after explicit confirmation', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    create();
    component.cancelItem('item-id');
    fixture.detectChanges();
    expect(api.cancelItem).toHaveBeenCalledWith('sale-id', 'item-id', '"v7"');
    expect(toast.success).toHaveBeenCalledWith('Item cancelled successfully.');

    component.cancelSale();
    fixture.detectChanges();
    expect(api.cancel).toHaveBeenCalledWith('sale-id', '"v7"');
    expect(component.resource()?.sale.isCancelled).toBe(true);
    expect(toast.success).toHaveBeenCalledWith('Sale cancelled successfully.');
  });

  it('does nothing when commands are unavailable or confirmation is declined', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    create();
    component.cancelSale();
    component.cancelItem('item-id');
    component.deleteSale();
    expect(api.cancel).not.toHaveBeenCalled();
    expect(api.cancelItem).not.toHaveBeenCalled();
    expect(api.delete).not.toHaveBeenCalled();

    component.resource.set(null);
    component.cancelSale();
    component.cancelItem('item-id');
    component.deleteSale();
    component.resource.set(saleResource());
    component.actionPending.set(true);
    component.cancelSale();
    component.cancelItem('item-id');
    component.deleteSale();
    expect(api.cancel).not.toHaveBeenCalled();
  });

  it('deletes the sale and returns to the list', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    create();
    component.deleteSale();
    expect(api.delete).toHaveBeenCalledWith('sale-id', '"v7"');
    expect(toast.success).toHaveBeenCalledWith('Sale deleted successfully.');
    expect(router.navigateByUrl).toHaveBeenCalledWith('/sales?_page=2');
    expect(component.actionPending()).toBe(false);
  });

  it('preserves a concurrency error until the user reloads current data', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    api.cancel.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 412, statusText: 'Precondition Failed' })),
    );
    create();
    component.cancelSale();
    fixture.detectChanges();
    expect(component.conflict()).toBe(true);
    expect(component.actionError()).toContain('changed after you opened');
    component.refreshAfterConflict();
    fixture.detectChanges();
    expect(component.conflict()).toBe(false);
    expect(component.actionError()).toBe('');
    expect(api.get).toHaveBeenCalledTimes(2);
  });
});

function saleResource(): SaleResource {
  return {
    etag: '"v7"',
    sale: {
      id: 'sale-id',
      saleNumber: 'SALE-1',
      saleDate: '2026-09-24T10:00:00Z',
      customer: { externalId: 'C-1', name: 'Customer' },
      branch: { externalId: 'B-1', name: 'Branch' },
      totalAmount: 36,
      isCancelled: false,
      cancelledAt: null,
      createdAt: '2026-09-24T10:00:00Z',
      updatedAt: '2026-09-24T10:00:01Z',
      version: 7,
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
    },
  };
}
