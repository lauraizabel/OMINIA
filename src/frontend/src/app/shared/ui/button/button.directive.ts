import { Directive, input } from '@angular/core';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'link';
export type ButtonSize = 'sm' | 'md';

@Directive({
  selector: 'button[appButton], a[appButton]',
  host: {
    class: 'ui-button',
    '[class.ui-button--primary]': "variant() === 'primary' || variant() === ''",
    '[class.ui-button--secondary]': "variant() === 'secondary'",
    '[class.ui-button--ghost]': "variant() === 'ghost'",
    '[class.ui-button--danger]': "variant() === 'danger'",
    '[class.ui-button--link]': "variant() === 'link'",
    '[class.ui-button--small]': "size() === 'sm'",
    '[class.ui-button--loading]': 'loading()',
    '[attr.aria-busy]': 'loading() || null',
  },
})
export class ButtonDirective {
  readonly variant = input<ButtonVariant | ''>('primary', { alias: 'appButton' });
  readonly size = input<ButtonSize>('md');
  readonly loading = input(false);
}
