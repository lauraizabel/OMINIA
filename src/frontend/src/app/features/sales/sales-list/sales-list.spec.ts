import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, Router } from '@angular/router';
import { BehaviorSubject, NEVER, of, throwError } from 'rxjs';
import { SalesApiService } from '../data-access/sales-api.service';
import { PagedSales } from '../data-access/sales.models';
import { SalesList, toLocalDateInput } from './sales-list';

describe('SalesList', () => {
  let fixture: ComponentFixture<SalesList>;
  let component: SalesList;
  let params: BehaviorSubject<ParamMap>;
  let api: { list: ReturnType<typeof vi.fn> };
  let router: { url: string; navigate: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    params = new BehaviorSubject(convertToParamMap({}));
    api = { list: vi.fn().mockReturnValue(of(page())) };
    router = { url: '/sales?_page=1', navigate: vi.fn().mockResolvedValue(true) };
    await TestBed.configureTestingModule({
      imports: [SalesList],
      providers: [
        { provide: SalesApiService, useValue: api },
        { provide: ActivatedRoute, useValue: { queryParamMap: params } },
        { provide: Router, useValue: router },
      ],
    }).compileComponents();
  });

  function create(): void {
    fixture = TestBed.createComponent(SalesList);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  it('loads URL filters, clamps invalid paging values, and renders results', () => {
    params.next(
      convertToParamMap({
        _page: '-2',
        _size: '500',
        _order: 'totalAmount asc',
        saleNumber: ' SALE-* ',
        customerExternalId: 'C-1',
        branchExternalId: 'B-1',
        isCancelled: 'false',
        _minSaleDate: '2026-09-24T03:00:00.000Z',
        _maxSaleDate: 'invalid',
        _minTotalAmount: '10',
        _maxTotalAmount: '100',
      }),
    );
    create();

    expect(component.query()).toMatchObject({
      page: 1,
      size: 100,
      saleNumber: 'SALE-*',
      isCancelled: false,
    });
    expect(component.filters.controls.status.value).toBe('active');
    expect(component.filters.controls.maxSaleDate.value).toBe('');
    expect(component.hasActiveFilters()).toBe(true);
    expect(component.loading()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('SALE-1');
  });

  it('serializes trimmed filters and local date boundaries into navigation', () => {
    create();
    component.filters.patchValue({
      saleNumber: ' SALE-2 ',
      status: 'cancelled',
      minSaleDate: '2026-09-24',
      maxSaleDate: '2026-09-25',
      minTotalAmount: '  ',
      order: 'saleNumber asc',
      size: 25,
    });

    component.applyFilters();

    const options = router.navigate.mock.calls.at(-1)?.[1];
    expect(options.queryParams).toMatchObject({
      _page: 1,
      _size: 25,
      saleNumber: 'SALE-2',
      isCancelled: true,
      _minTotalAmount: null,
    });
    expect(options.queryParams._minSaleDate).toBe(new Date('2026-09-24T00:00:00').toISOString());
    expect(options.queryParams._maxSaleDate).toBe(
      new Date('2026-09-25T23:59:59.999').toISOString(),
    );
  });

  it('clears filters, changes page size, and respects pagination boundaries', () => {
    api.list.mockReturnValue(of(page({ currentPage: 2, totalPages: 3 })));
    params.next(convertToParamMap({ _page: '2', _size: '25' }));
    create();

    component.previousPage();
    component.nextPage();
    component.changePageSize(50);
    component.clearFilters();

    expect(router.navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParams: { _page: 1 }, queryParamsHandling: 'merge' }),
    );
    expect(router.navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParams: { _page: 3 }, queryParamsHandling: 'merge' }),
    );
    expect(component.filters.controls.size.value).toBe(10);
    expect(component.hasActiveFilters()).toBe(false);
  });

  it('does not navigate beyond the first or last page', () => {
    create();
    component.previousPage();
    component.nextPage();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('redirects to the final available page when the requested page is out of range', () => {
    params.next(convertToParamMap({ _page: '9' }));
    api.list.mockReturnValue(of(page({ currentPage: 9, totalPages: 2 })));
    create();
    expect(router.navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParams: { _page: 2 } }),
    );
  });

  it('shows a useful load error and retries the request', () => {
    api.list
      .mockReturnValueOnce(
        throwError(() => new HttpErrorResponse({ status: 503, statusText: 'Unavailable' })),
      )
      .mockReturnValueOnce(of(page()));
    create();
    expect(component.error()).toContain('required service');

    component.retry();
    expect(api.list).toHaveBeenCalledTimes(2);
    expect(component.result()?.data[0].saleNumber).toBe('SALE-1');
  });

  it('renders loading, unfiltered empty, and filtered empty states', () => {
    api.list.mockReturnValue(NEVER);
    create();
    expect(fixture.nativeElement.querySelector('[aria-label="Loading sales"]')).not.toBeNull();

    fixture.destroy();
    api.list.mockReturnValue(of(page({ data: [], totalItems: 0, totalPages: 0 })));
    create();
    expect(fixture.nativeElement.textContent).toContain('No sales yet');
    expect(fixture.nativeElement.textContent).toContain('Create sale');

    fixture.destroy();
    params.next(convertToParamMap({ saleNumber: 'missing' }));
    create();
    expect(fixture.nativeElement.textContent).toContain('No sales found');
    expect(fixture.nativeElement.textContent).toContain('Clear filters');
  });

  it('normalizes optional values and local dates defensively', () => {
    expect(toLocalDateInput(undefined)).toBe('');
    expect(toLocalDateInput('not-a-date')).toBe('');
    expect(toLocalDateInput('2026-09-24T12:00:00Z')).toMatch(/^2026-09-24$/);
  });
});

function page(overrides: Partial<PagedSales> = {}): PagedSales {
  return {
    data: [
      {
        id: 'sale-id',
        saleNumber: 'SALE-1',
        saleDate: '2026-09-24T10:00:00Z',
        customer: { externalId: 'C-1', name: 'Customer' },
        branch: { externalId: 'B-1', name: 'Branch' },
        totalAmount: 40,
        isCancelled: false,
        updatedAt: '2026-09-24T10:00:01Z',
        version: 1,
      },
    ],
    totalItems: 1,
    currentPage: 1,
    totalPages: 1,
    ...overrides,
  };
}
