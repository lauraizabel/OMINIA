import { CurrencyPipe, DatePipe, PercentPipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, EMPTY, finalize, Subject, switchMap } from 'rxjs';
import { safeReturnUrl } from '../../../core/auth/return-url';
import { SalesApiService } from '../data-access/sales-api.service';
import { SaleResource } from '../data-access/sales.models';
import { mapSaleError } from '../shared/sale-errors';

@Component({
  selector: 'app-sale-detail',
  imports: [CurrencyPipe, DatePipe, PercentPipe, RouterLink],
  templateUrl: './sale-detail.html',
  styleUrl: './sale-detail.scss',
})
export class SaleDetail {
  private readonly api = inject(SalesApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly reload = new Subject<void>();

  readonly resource = signal<SaleResource | null>(null);
  readonly loading = signal(true);
  readonly actionPending = signal(false);
  readonly error = signal('');
  readonly actionError = signal('');
  readonly conflict = signal(false);
  readonly returnUrl = safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));

  constructor() {
    const destroyRef = inject(DestroyRef);
    this.reload
      .pipe(
        switchMap(() => {
          this.loading.set(true);
          this.error.set('');
          const id = this.route.snapshot.paramMap.get('id') ?? '';
          return this.api.get(id).pipe(
            catchError((error: unknown) => {
              this.error.set(mapSaleError(error).message);
              return EMPTY;
            }),
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe((resource) => this.accept(resource));
    this.reload.next();
  }

  back(): void {
    void this.router.navigateByUrl(this.returnUrl);
  }

  retry(): void {
    this.reload.next();
  }

  refreshAfterConflict(): void {
    this.conflict.set(false);
    this.actionError.set('');
    this.reload.next();
  }

  cancelSale(): void {
    const resource = this.resource();
    if (!resource || resource.sale.isCancelled || this.actionPending()) return;
    if (!window.confirm(`Cancel sale ${resource.sale.saleNumber} and all active items?`)) return;
    this.runCommand(this.api.cancel(resource.sale.id, resource.etag));
  }

  cancelItem(itemId: string): void {
    const resource = this.resource();
    if (!resource || this.actionPending()) return;
    if (!window.confirm('Cancel this item? This action cannot be undone.')) return;
    this.runCommand(this.api.cancelItem(resource.sale.id, itemId, resource.etag));
  }

  deleteSale(): void {
    const resource = this.resource();
    if (!resource || this.actionPending()) return;
    if (
      !window.confirm(
        `Delete sale ${resource.sale.saleNumber}? It will disappear from public reads.`,
      )
    )
      return;
    this.actionPending.set(true);
    this.clearActionError();
    this.api
      .delete(resource.sale.id, resource.etag)
      .pipe(finalize(() => this.actionPending.set(false)))
      .subscribe({
        next: () => void this.router.navigateByUrl(this.returnUrl),
        error: (error: unknown) => this.showActionError(error),
      });
  }

  private runCommand(command: ReturnType<SalesApiService['cancel']>): void {
    this.actionPending.set(true);
    this.clearActionError();
    command.pipe(finalize(() => this.actionPending.set(false))).subscribe({
      next: (resource) => this.accept(resource),
      error: (error: unknown) => this.showActionError(error),
    });
  }

  private accept(resource: SaleResource): void {
    this.resource.set(resource);
    this.conflict.set(false);
    this.actionError.set('');
  }

  private clearActionError(): void {
    this.actionError.set('');
    this.conflict.set(false);
  }

  private showActionError(error: unknown): void {
    const state = mapSaleError(error);
    this.actionError.set(state.message);
    this.conflict.set(state.conflict);
  }
}
