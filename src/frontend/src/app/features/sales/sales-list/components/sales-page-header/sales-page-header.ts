import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { Icon } from '../../../../../shared/ui/icon/icon';
import { PageHeader } from '../../../../../shared/ui/page-header/page-header';

@Component({
  selector: 'app-sales-page-header',
  imports: [ButtonDirective, Icon, PageHeader, RouterLink],
  template: `
    <app-page-header title="Sales" description="Manage and monitor commercial sales.">
      <a appButton routerLink="/sales/new" [queryParams]="{ returnUrl: returnUrl() }">
        <app-icon name="plus" /> New sale
      </a>
    </app-page-header>
  `,
  styles: ``,
})
export class SalesPageHeader {
  readonly returnUrl = input.required<string>();
}
