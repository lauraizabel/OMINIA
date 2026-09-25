import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-sales-pagination',
  imports: [FormsModule],
  template: `
    <footer aria-label="Sales pagination">
      <span class="result-count"
        >{{ totalItems() }} {{ totalItems() === 1 ? 'result' : 'results' }}</span
      >
      <div class="controls">
        <label
          >Rows:
          <select
            [ngModel]="pageSize()"
            (ngModelChange)="pageSizeChange.emit(+$event)"
            aria-label="Rows per page"
          >
            <option [ngValue]="10">10</option>
            <option [ngValue]="25">25</option>
            <option [ngValue]="50">50</option>
          </select>
        </label>
        <div class="page-actions">
          <button
            type="button"
            (click)="previous.emit()"
            [disabled]="currentPage() <= 1"
            aria-label="Previous page"
          >
            <span aria-hidden="true">‹</span> Previous
          </button>
          <span class="current-page" aria-current="page">{{ currentPage() }}</span>
          <button type="button" (click)="next.emit()" [disabled]="currentPage() >= totalPages()">
            Next <span aria-hidden="true">›</span>
          </button>
        </div>
      </div>
    </footer>
  `,
  styles: `
    footer {
      display: flex;
      min-height: 4rem;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 0.75rem 1rem;
      border-top: 1px solid var(--border);
      color: var(--muted);
      font-size: 0.75rem;
    }
    .controls,
    .controls label,
    .page-actions {
      display: flex;
      align-items: center;
    }
    .controls {
      gap: 1.5rem;
    }
    .controls label {
      position: relative;
      gap: 0.5rem;
    }
    .controls label::after {
      position: absolute;
      top: 50%;
      right: 0.6rem;
      width: 0.35rem;
      height: 0.35rem;
      border-right: 1.5px solid var(--color-text-muted);
      border-bottom: 1.5px solid var(--color-text-muted);
      content: '';
      pointer-events: none;
      transform: translateY(-70%) rotate(45deg);
    }
    .controls select {
      height: 2rem;
      padding: 0 1.6rem 0 0.5rem;
      border: 1px solid var(--border);
      border-radius: 0.375rem;
      color: var(--ink);
      background: var(--color-surface);
      font-size: 0.75rem;
      appearance: none;
    }
    .page-actions {
      gap: 0.25rem;
    }
    .page-actions button {
      display: inline-flex;
      height: 2rem;
      align-items: center;
      gap: 0.3rem;
      padding: 0 0.6rem;
      border: 0;
      border-radius: 0.375rem;
      color: var(--color-text-secondary);
      background: transparent;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
    }
    .page-actions button:hover:not(:disabled) {
      background: var(--color-surface-hover);
    }
    .page-actions button:disabled {
      color: var(--color-text-muted);
      cursor: not-allowed;
    }
    .current-page {
      display: grid;
      width: 2rem;
      height: 2rem;
      place-items: center;
      border: 1px solid var(--color-info-border);
      border-radius: 0.375rem;
      color: var(--primary);
      background: var(--color-info-subtle);
      font-weight: 650;
    }
    @media (max-width: 600px) {
      footer {
        align-items: flex-start;
        flex-direction: column;
      }
      .controls {
        width: 100%;
        justify-content: space-between;
        gap: 0.5rem;
      }
      .page-actions button {
        height: 2.75rem;
        padding: 0 0.4rem;
      }
      .controls select,
      .current-page {
        height: 2.75rem;
      }
    }
  `,
})
export class SalesPagination {
  readonly totalItems = input.required<number>();
  readonly currentPage = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly previous = output<void>();
  readonly next = output<void>();
  readonly pageSizeChange = output<number>();
}
