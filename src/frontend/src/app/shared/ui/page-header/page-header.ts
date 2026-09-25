import { Component, input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  template: `
    <header class="page-header">
      <div class="page-header__copy">
        @if (eyebrow()) {
          <p class="page-header__eyebrow">{{ eyebrow() }}</p>
        }
        <h1>{{ title() }}</h1>
        @if (description()) {
          <p class="page-header__description">{{ description() }}</p>
        }
      </div>
      <div class="page-header__actions"><ng-content /></div>
    </header>
  `,
  styles: `
    .page-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--space-8);
      margin-bottom: var(--space-6);
    }
    .page-header__copy {
      min-width: 0;
    }
    .page-header__eyebrow {
      margin: 0 0 var(--space-2);
      color: var(--color-primary);
      font-size: var(--font-size-xs);
      font-weight: 700;
      letter-spacing: 0.1em;
      text-transform: uppercase;
    }
    h1 {
      margin: 0;
      color: var(--color-text-primary);
      font: 700 clamp(1.75rem, 4vw, 2.25rem) / 1.15 var(--font-display);
      letter-spacing: -0.035em;
    }
    .page-header__description {
      max-width: 48rem;
      margin: var(--space-2) 0 0;
      color: var(--color-text-secondary);
      font-size: var(--font-size-md);
      line-height: 1.55;
    }
    .page-header__actions {
      display: flex;
      flex: none;
      align-items: center;
      gap: var(--space-2);
    }
    .page-header__actions:empty {
      display: none;
    }
    @media (max-width: 560px) {
      .page-header {
        align-items: stretch;
        flex-direction: column;
        gap: var(--space-4);
      }
      .page-header__actions {
        align-items: stretch;
      }
      .page-header__actions ::ng-deep .ui-button {
        flex: 1;
        justify-content: center;
      }
    }
  `,
})
export class PageHeader {
  readonly eyebrow = input('');
  readonly title = input.required<string>();
  readonly description = input('');
}
