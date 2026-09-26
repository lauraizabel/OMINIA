import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { OverlayMenuCoordinator } from '../../../../../shared/overlay-menu-coordinator.service';
import { SalesFilters, SalesFiltersForm } from './sales-filters';

describe('SalesFilters', () => {
  let fixture: ComponentFixture<SalesFilters>;
  let component: SalesFilters;
  let form: SalesFiltersForm;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SalesFilters] }).compileComponents();
    fixture = TestBed.createComponent(SalesFilters);
    component = fixture.componentInstance;
    form = new FormGroup({
      saleNumber: new FormControl('', { nonNullable: true }),
      customerExternalId: new FormControl('', { nonNullable: true }),
      branchExternalId: new FormControl('', { nonNullable: true }),
      status: new FormControl('', { nonNullable: true }),
      minSaleDate: new FormControl('', { nonNullable: true }),
      maxSaleDate: new FormControl('', { nonNullable: true }),
      minTotalAmount: new FormControl('', { nonNullable: true }),
      maxTotalAmount: new FormControl('', { nonNullable: true }),
      order: new FormControl('saleDate desc', { nonNullable: true }),
      size: new FormControl(10, { nonNullable: true }),
    });
    fixture.componentRef.setInput('form', form);
    fixture.detectChanges();
  });

  it('builds removable chips and counts active and advanced filters', () => {
    form.patchValue({
      saleNumber: 'SALE',
      customerExternalId: 'C-1',
      branchExternalId: 'B-1',
      status: 'active',
      minSaleDate: '2026-09-01',
      maxSaleDate: '2026-09-30',
      minTotalAmount: '10',
      maxTotalAmount: '100',
      order: 'totalAmount desc',
    });

    expect(component.activeFilterCount()).toBe(9);
    expect(component.moreFilterCount()).toBe(5);
    expect(component.filterChips().map((chip) => chip.label)).toEqual([
      'Sale: SALE',
      'Status: Active',
      '01/09/2026 – 30/09/2026',
      'Customer: C-1',
      'Branch: B-1',
      'Min. total: 10',
      'Max. total: 100',
      'Custom order',
    ]);
  });

  it('formats open-ended ranges and cancelled status', () => {
    form.patchValue({ status: 'cancelled', minSaleDate: '2026-09-01' });
    expect(component.dateLabel()).toBe('From 01/09/2026');
    expect(component.filterChips()[0].label).toBe('Status: Cancelled');
    form.patchValue({ minSaleDate: '', maxSaleDate: '2026-09-30' });
    expect(component.dateLabel()).toBe('Until 30/09/2026');
    form.patchValue({ maxSaleDate: '' });
    expect(component.dateLabel()).toBe('Date range');
  });

  it('clears a chip using the correct defaults and emits an application', () => {
    const emitted = vi.fn();
    component.filtersApplied.subscribe(emitted);
    form.patchValue({ saleNumber: 'SALE', order: 'totalAmount desc' });
    component.clearFilter(['saleNumber', 'order']);
    expect(form.controls.saleNumber.value).toBe('');
    expect(form.controls.order.value).toBe('saleDate desc');
    expect(emitted).toHaveBeenCalledOnce();
  });

  it('coordinates menus, closes on Escape, and applies filters', () => {
    const coordinator = TestBed.inject(OverlayMenuCoordinator);
    const preventDefault = vi.fn();
    const emitted = vi.fn();
    component.filtersApplied.subscribe(emitted);

    component.toggleMenu('date');
    expect(component.isMenuOpen('date')).toBe(true);
    component.toggleMenu('more');
    expect(component.isMenuOpen('date')).toBe(false);
    expect(component.isMenuOpen('more')).toBe(true);
    component.handleOverlayKeydown({ key: 'Enter', preventDefault } as unknown as KeyboardEvent);
    expect(preventDefault).not.toHaveBeenCalled();
    component.handleOverlayKeydown({ key: 'Escape', preventDefault } as unknown as KeyboardEvent);
    expect(preventDefault).toHaveBeenCalledOnce();
    expect(coordinator.isOpen('sales-filters-more')).toBe(false);

    component.toggleMenu('date');
    component.apply();
    expect(component.isMenuOpen('date')).toBe(false);
    expect(emitted).toHaveBeenCalledOnce();
  });

  it('renders both overlay forms and active chips', async () => {
    form.patchValue({
      saleNumber: 'SALE',
      customerExternalId: 'C-1',
      branchExternalId: 'B-1',
      minSaleDate: '2026-09-01',
      maxSaleDate: '2026-09-30',
      minTotalAmount: '10',
      maxTotalAmount: '100',
      order: 'totalAmount desc',
    });
    component.toggleMenu('date');
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.body.textContent).toContain('Date range');

    component.toggleMenu('more');
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.body.textContent).toContain('Customer ID');
    expect(fixture.nativeElement.textContent).toContain('Sale: SALE');
    expect(fixture.nativeElement.textContent).toContain('Clear all');
  });
});
