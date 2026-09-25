import { Component, input } from '@angular/core';

@Component({
  selector: 'app-status-badge',
  template: `<span class="badge" [class.cancelled]="cancelled()"
    ><span aria-hidden="true"></span>{{ cancelled() ? 'Cancelled' : 'Active' }}</span
  >`,
  styles: `
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      padding: 0.25rem 0.5rem;
      border: 1px solid var(--color-success-border);
      border-radius: var(--radius-pill);
      color: var(--color-success-text);
      background: var(--color-success-subtle);
      font-size: 0.7rem;
      font-weight: 650;
      line-height: 1;
    }
    .badge span {
      width: 0.375rem;
      height: 0.375rem;
      border-radius: 50%;
      background: var(--color-success-text);
    }
    .badge.cancelled {
      border-color: var(--color-danger-border);
      color: var(--color-danger-text);
      background: var(--color-danger-subtle);
    }
    .badge.cancelled span {
      background: var(--color-danger);
    }
  `,
})
export class StatusBadge {
  readonly cancelled = input.required<boolean>();
}
