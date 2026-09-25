import { DatePipe } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MoneyPipe } from '../../../../../shared/ui/money.pipe';
import { Icon } from '../../../../../shared/ui/icon/icon';
import { PagedSales } from '../../../data-access/sales.models';
import { SalesPagination } from '../sales-pagination/sales-pagination';
import { StatusBadge } from '../status-badge/status-badge';

@Component({
  selector: 'app-sales-table',
  imports: [DatePipe, Icon, MoneyPipe, RouterLink, SalesPagination, StatusBadge],
  templateUrl: './sales-table.html',
  styleUrl: './sales-table.scss',
})
export class SalesTable {
  readonly result = input.required<PagedSales>();
  readonly pageSize = input.required<number>();
  readonly returnUrl = input.required<string>();
  readonly previousPage = output<void>();
  readonly nextPage = output<void>();
  readonly pageSizeChange = output<number>();
}
