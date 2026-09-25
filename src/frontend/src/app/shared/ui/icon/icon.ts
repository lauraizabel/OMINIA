import { Component, input } from '@angular/core';

export type IconName =
  'arrow-left' | 'arrow-right' | 'check' | 'close' | 'logout' | 'plus' | 'trash';

@Component({
  selector: 'app-icon',
  template: `
    <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
      @switch (name()) {
        @case ('arrow-left') {
          <path d="m15 18-6-6 6-6M9 12h10" />
        }
        @case ('arrow-right') {
          <path d="m9 18 6-6-6-6m6 6H5" />
        }
        @case ('check') {
          <path d="m5 12 4 4L19 6" />
        }
        @case ('close') {
          <path d="M6 6l12 12M18 6 6 18" />
        }
        @case ('logout') {
          <path d="M10 17l5-5-5-5m5 5H3m11-8h5a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-5" />
        }
        @case ('plus') {
          <path d="M12 5v14M5 12h14" />
        }
        @case ('trash') {
          <path d="M4 7h16M9 7V4h6v3m3 0-1 13H7L6 7m4 4v5m4-5v5" />
        }
      }
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      width: 1rem;
      height: 1rem;
      flex: none;
    }
    svg {
      width: 100%;
      height: 100%;
      stroke: currentColor;
      stroke-linecap: round;
      stroke-linejoin: round;
      stroke-width: 1.8;
    }
  `,
})
export class Icon {
  readonly name = input.required<IconName>();
}
