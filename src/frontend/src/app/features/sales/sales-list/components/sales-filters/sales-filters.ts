import { ConnectedPosition, OverlayModule } from '@angular/cdk/overlay';
import { Component, inject, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { OverlayMenuCoordinator } from '../../../../../shared/overlay-menu-coordinator.service';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { Icon } from '../../../../../shared/ui/icon/icon';

export type SalesFiltersForm = FormGroup<{
  saleNumber: FormControl<string>;
  customerExternalId: FormControl<string>;
  branchExternalId: FormControl<string>;
  status: FormControl<string>;
  minSaleDate: FormControl<string>;
  maxSaleDate: FormControl<string>;
  minTotalAmount: FormControl<string>;
  maxTotalAmount: FormControl<string>;
  order: FormControl<string>;
  size: FormControl<number>;
}>;

type FilterControlName = keyof SalesFiltersForm['controls'];

@Component({
  selector: 'app-sales-filters',
  imports: [ButtonDirective, Icon, OverlayModule, ReactiveFormsModule],
  templateUrl: './sales-filters.html',
  styleUrl: './sales-filters.scss',
})
export class SalesFilters {
  private readonly menuCoordinator = inject(OverlayMenuCoordinator);
  private readonly menuIds = {
    date: 'sales-filters-date',
    more: 'sales-filters-more',
  } as const;

  readonly form = input.required<SalesFiltersForm>();
  readonly filtersApplied = output<void>();
  readonly filtersCleared = output<void>();
  readonly overlayPositions: ConnectedPosition[] = [
    {
      originX: 'end',
      originY: 'bottom',
      overlayX: 'end',
      overlayY: 'top',
      offsetY: 8,
    },
    {
      originX: 'end',
      originY: 'top',
      overlayX: 'end',
      overlayY: 'bottom',
      offsetY: -8,
    },
  ];

  isMenuOpen(menu: 'date' | 'more'): boolean {
    return this.menuCoordinator.isOpen(this.menuIds[menu]);
  }

  toggleMenu(menu: 'date' | 'more'): void {
    this.menuCoordinator.toggle(this.menuIds[menu]);
  }

  closeMenus(): void {
    this.menuCoordinator.close();
  }

  handleOverlayKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.closeMenus();
    }
  }

  activeFilterCount(): number {
    const value = this.form().getRawValue();
    return [
      value.saleNumber,
      value.customerExternalId,
      value.branchExternalId,
      value.status,
      value.minSaleDate,
      value.maxSaleDate,
      value.minTotalAmount,
      value.maxTotalAmount,
      value.order === 'saleDate desc' ? '' : value.order,
    ].filter((entry) => String(entry).trim()).length;
  }

  moreFilterCount(): number {
    const value = this.form().getRawValue();
    return [
      value.customerExternalId,
      value.branchExternalId,
      value.minTotalAmount,
      value.maxTotalAmount,
      value.order === 'saleDate desc' ? '' : value.order,
    ].filter((entry) => String(entry).trim()).length;
  }

  filterChips(): { label: string; controls: FilterControlName[] }[] {
    const value = this.form().getRawValue();
    const chips: { label: string; controls: FilterControlName[] }[] = [];
    if (value.saleNumber)
      chips.push({ label: `Sale: ${value.saleNumber}`, controls: ['saleNumber'] });
    if (value.status) {
      chips.push({
        label: `Status: ${value.status === 'active' ? 'Active' : 'Cancelled'}`,
        controls: ['status'],
      });
    }
    if (value.minSaleDate || value.maxSaleDate) {
      chips.push({ label: this.dateLabel(), controls: ['minSaleDate', 'maxSaleDate'] });
    }
    if (value.customerExternalId) {
      chips.push({
        label: `Customer: ${value.customerExternalId}`,
        controls: ['customerExternalId'],
      });
    }
    if (value.branchExternalId) {
      chips.push({ label: `Branch: ${value.branchExternalId}`, controls: ['branchExternalId'] });
    }
    if (value.minTotalAmount) {
      chips.push({ label: `Min. total: ${value.minTotalAmount}`, controls: ['minTotalAmount'] });
    }
    if (value.maxTotalAmount) {
      chips.push({ label: `Max. total: ${value.maxTotalAmount}`, controls: ['maxTotalAmount'] });
    }
    if (value.order !== 'saleDate desc') {
      chips.push({ label: 'Custom order', controls: ['order'] });
    }
    return chips;
  }

  clearFilter(controls: FilterControlName[]): void {
    for (const controlName of controls) {
      const defaultValue = controlName === 'order' ? 'saleDate desc' : '';
      this.form().controls[controlName].setValue(defaultValue as never);
    }
    this.filtersApplied.emit();
  }

  dateLabel(): string {
    const { minSaleDate, maxSaleDate } = this.form().getRawValue();
    if (!minSaleDate && !maxSaleDate) return 'Date range';
    if (minSaleDate && maxSaleDate) return `${shortDate(minSaleDate)} – ${shortDate(maxSaleDate)}`;
    return minSaleDate ? `From ${shortDate(minSaleDate)}` : `Until ${shortDate(maxSaleDate)}`;
  }

  apply(): void {
    this.closeMenus();
    this.filtersApplied.emit();
  }
}

function shortDate(value: string): string {
  const [year, month, day] = value.split('-');
  return `${day}/${month}/${year}`;
}
