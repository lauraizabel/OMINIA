import { Component, inject } from '@angular/core';
import { Icon } from '../icon/icon';
import { ToastService } from './toast.service';

@Component({
  selector: 'app-toast-viewport',
  imports: [Icon],
  template: `
    <aside class="toast-viewport" aria-label="Notifications" aria-live="polite">
      @for (toast of toastService.messages(); track toast.id) {
        <div class="toast" [class.toast--success]="toast.tone === 'success'">
          <span class="toast__icon"><app-icon name="check" /></span>
          <span>{{ toast.message }}</span>
          <button
            type="button"
            (click)="toastService.dismiss(toast.id)"
            aria-label="Dismiss notification"
          >
            <app-icon name="close" />
          </button>
        </div>
      }
    </aside>
  `,
  styles: `
    .toast-viewport {
      position: fixed;
      z-index: 1000;
      right: var(--space-6);
      bottom: var(--space-6);
      display: grid;
      width: min(24rem, calc(100vw - var(--space-8)));
      gap: var(--space-2);
      pointer-events: none;
    }
    .toast {
      display: grid;
      grid-template-columns: auto 1fr auto;
      align-items: center;
      gap: var(--space-3);
      padding: var(--space-3) var(--space-4);
      border: 1px solid var(--color-info-border);
      border-radius: var(--radius-md);
      color: var(--color-info-text);
      background: var(--color-surface);
      box-shadow: var(--shadow-lg);
      font-size: var(--font-size-sm);
      pointer-events: auto;
    }
    .toast--success {
      border-color: var(--color-success-border);
      color: var(--color-success-text);
    }
    .toast__icon {
      display: grid;
      width: 1.5rem;
      height: 1.5rem;
      place-items: center;
      border-radius: 50%;
      background: var(--color-success-subtle);
      font-weight: 800;
    }
    button {
      width: 2rem;
      height: 2rem;
      border: 0;
      border-radius: var(--radius-sm);
      color: inherit;
      background: transparent;
      cursor: pointer;
    }
    button:hover {
      background: var(--color-surface-subtle);
    }
    @media (max-width: 560px) {
      .toast-viewport {
        right: var(--space-4);
        bottom: var(--space-4);
      }
    }
  `,
})
export class ToastViewport {
  protected readonly toastService = inject(ToastService);
}
