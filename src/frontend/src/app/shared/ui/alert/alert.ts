import { Component, input } from '@angular/core';

@Component({
  selector: 'app-alert',
  template: `
    <div class="alert" [class]="'alert alert--' + tone()" role="alert" tabindex="-1">
      <div class="alert__content">
        @if (title()) {
          <strong>{{ title() }}</strong>
        }
        <span><ng-content /></span>
      </div>
      <div class="alert__action"><ng-content select="[alertAction]" /></div>
    </div>
  `,
  styles: `
    .alert {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-4);
      margin-bottom: var(--space-4);
      padding: var(--space-4);
      border: 1px solid;
      border-radius: var(--radius-md);
      font-size: var(--font-size-sm);
    }
    .alert--danger {
      border-color: var(--color-danger-border);
      color: var(--color-danger-text);
      background: var(--color-danger-subtle);
    }
    .alert--warning {
      border-color: var(--color-warning-border);
      color: var(--color-warning-text);
      background: var(--color-warning-subtle);
    }
    .alert--info {
      border-color: var(--color-info-border);
      color: var(--color-info-text);
      background: var(--color-info-subtle);
    }
    .alert__content {
      display: grid;
      gap: var(--space-1);
      line-height: 1.45;
    }
    .alert__action:empty {
      display: none;
    }
  `,
})
export class Alert {
  readonly tone = input<'danger' | 'warning' | 'info'>('danger');
  readonly title = input('');
}
