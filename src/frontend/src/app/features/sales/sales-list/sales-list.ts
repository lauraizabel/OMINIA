import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { catchError, distinctUntilChanged, map, of, startWith, Subject, switchMap } from 'rxjs';
import { ButtonDirective } from '../../../shared/ui/button/button.directive';
import { SalesApiService } from '../data-access/sales-api.service';
import { PagedSales, SaleListQuery } from '../data-access/sales.models';
import { mapSaleError } from '../shared/sale-errors';
import { SalesFilters, SalesFiltersForm } from './components/sales-filters/sales-filters';
import { SalesPageHeader } from './components/sales-page-header/sales-page-header';
import { SalesTable } from './components/sales-table/sales-table';

@Component({
  selector: 'app-sales-list',
  imports: [ButtonDirective, RouterLink, SalesFilters, SalesPageHeader, SalesTable],
  templateUrl: './sales-list.html',
  styleUrl: './sales-list.scss',
})
export class SalesList {
  private readonly api = inject(SalesApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly retryRequests = new Subject<void>();

  readonly loading = signal(true);
  readonly result = signal<PagedSales | null>(null);
  readonly error = signal('');
  readonly query = signal<SaleListQuery>(defaultQuery());
  get returnUrl(): string {
    return this.router.url;
  }

  readonly filters: SalesFiltersForm = new FormGroup({
    saleNumber: new FormControl('', { nonNullable: true }),
    customerExternalId: new FormControl('', { nonNullable: true }),
    branchExternalId: new FormControl('', { nonNullable: true }),
    status: new FormControl('', { nonNullable: true }),
    minSaleDate: new FormControl('', { nonNullable: true }),
    maxSaleDate: new FormControl('', { nonNullable: true }),
    minTotalAmount: new FormControl('', { nonNullable: true }),
    maxTotalAmount: new FormControl('', { nonNullable: true }),
    order: new FormControl('saleDate desc', { nonNullable: true }),
    size: new FormControl(10, { nonNullable: true }),
  });

  constructor() {
    this.route.queryParamMap
      .pipe(
        map(toQuery),
        distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
        takeUntilDestroyed(),
      )
      .subscribe((query) => {
        this.query.set(query);
        this.patchFilters(query);
      });

    this.retryRequests
      .pipe(
        startWith(undefined),
        switchMap(() =>
          this.route.queryParamMap.pipe(
            map(toQuery),
            distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
          ),
        ),
        switchMap((query) => {
          this.loading.set(true);
          this.error.set('');
          return this.api.list(query).pipe(
            catchError((error: unknown) => {
              this.error.set(mapSaleError(error).message);
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.loading.set(false);
        if (!result) return;
        if (result.totalPages > 0 && result.currentPage > result.totalPages) {
          void this.goToPage(result.totalPages);
          return;
        }
        this.result.set(result);
      });
  }

  applyFilters(): void {
    const value = this.filters.getRawValue();
    const params: Record<string, string | number | boolean | null> = {
      _page: 1,
      _size: value.size,
      _order: value.order,
      saleNumber: clean(value.saleNumber),
      customerExternalId: clean(value.customerExternalId),
      branchExternalId: clean(value.branchExternalId),
      isCancelled: value.status === '' ? null : value.status === 'cancelled',
      _minSaleDate: dateStart(value.minSaleDate),
      _maxSaleDate: dateEnd(value.maxSaleDate),
      _minTotalAmount: clean(value.minTotalAmount),
      _maxTotalAmount: clean(value.maxTotalAmount),
    };
    void this.router.navigate([], { relativeTo: this.route, queryParams: params });
  }

  clearFilters(): void {
    this.filters.reset({ order: 'saleDate desc', size: 10 });
    this.applyFilters();
  }

  retry(): void {
    this.retryRequests.next();
  }

  previousPage(): void {
    if (this.query().page > 1) void this.goToPage(this.query().page - 1);
  }

  nextPage(): void {
    const totalPages = this.result()?.totalPages ?? 0;
    if (this.query().page < totalPages) void this.goToPage(this.query().page + 1);
  }

  changePageSize(size: number): void {
    this.filters.controls.size.setValue(size);
    this.applyFilters();
  }

  hasActiveFilters(): boolean {
    const query = this.query();
    return Boolean(
      query.saleNumber ||
      query.customerExternalId ||
      query.branchExternalId ||
      query.isCancelled !== undefined ||
      query.minSaleDate ||
      query.maxSaleDate ||
      query.minTotalAmount ||
      query.maxTotalAmount,
    );
  }

  private goToPage(page: number): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { _page: page },
      queryParamsHandling: 'merge',
    });
  }

  private patchFilters(query: SaleListQuery): void {
    this.filters.patchValue(
      {
        saleNumber: query.saleNumber ?? '',
        customerExternalId: query.customerExternalId ?? '',
        branchExternalId: query.branchExternalId ?? '',
        status: query.isCancelled === undefined ? '' : query.isCancelled ? 'cancelled' : 'active',
        minSaleDate: toLocalDateInput(query.minSaleDate),
        maxSaleDate: toLocalDateInput(query.maxSaleDate),
        minTotalAmount: query.minTotalAmount ?? '',
        maxTotalAmount: query.maxTotalAmount ?? '',
        order: query.order,
        size: query.size,
      },
      { emitEvent: false },
    );
  }
}

function toQuery(params: ParamMap): SaleListQuery {
  const page = positiveInt(params.get('_page'), 1);
  const size = Math.min(100, positiveInt(params.get('_size'), 10));
  const status = params.get('isCancelled');
  return {
    page,
    size,
    order: params.get('_order') || 'saleDate desc',
    saleNumber: optional(params.get('saleNumber')),
    customerExternalId: optional(params.get('customerExternalId')),
    branchExternalId: optional(params.get('branchExternalId')),
    isCancelled: status === 'true' ? true : status === 'false' ? false : undefined,
    minSaleDate: optional(params.get('_minSaleDate')),
    maxSaleDate: optional(params.get('_maxSaleDate')),
    minTotalAmount: optional(params.get('_minTotalAmount')),
    maxTotalAmount: optional(params.get('_maxTotalAmount')),
  };
}

function defaultQuery(): SaleListQuery {
  return { page: 1, size: 10, order: 'saleDate desc' };
}

function positiveInt(value: string | null, fallback: number): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function optional(value: string | null): string | undefined {
  return value?.trim() || undefined;
}

function clean(value: string): string | null {
  return value.trim() || null;
}

function dateStart(value: string): string | null {
  return value ? new Date(`${value}T00:00:00`).toISOString() : null;
}

function dateEnd(value: string): string | null {
  return value ? new Date(`${value}T23:59:59.999`).toISOString() : null;
}

export function toLocalDateInput(value: string | undefined): string {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const localDate = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return localDate.toISOString().slice(0, 10);
}
