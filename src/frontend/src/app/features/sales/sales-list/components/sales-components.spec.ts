import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PagedSales } from '../../data-access/sales.models';
import { SalesPageHeader } from './sales-page-header/sales-page-header';
import { SalesPagination } from './sales-pagination/sales-pagination';
import { SalesTable } from './sales-table/sales-table';
import { StatusBadge } from './status-badge/status-badge';

describe('sales list presentation components', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter([])] }));

  it('renders both status states', () => {
    const fixture = TestBed.createComponent(StatusBadge);
    fixture.componentRef.setInput('cancelled', false);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Active');
    expect(fixture.nativeElement.querySelector('.cancelled')).toBeNull();

    fixture.componentRef.setInput('cancelled', true);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Cancelled');
    expect(fixture.nativeElement.querySelector('.cancelled')).not.toBeNull();
  });

  it('emits pagination commands and disables unavailable directions', () => {
    const fixture = TestBed.createComponent(SalesPagination);
    setPaginationInputs(fixture, 1, 2);
    const component = fixture.componentInstance;
    const previous = vi.fn();
    const next = vi.fn();
    const size = vi.fn();
    component.previous.subscribe(previous);
    component.next.subscribe(next);
    component.pageSizeChange.subscribe(size);
    fixture.detectChanges();

    const buttons = fixture.nativeElement.querySelectorAll('button');
    expect(buttons[0].disabled).toBe(true);
    buttons[0].click();
    buttons[1].click();
    expect(previous).not.toHaveBeenCalled();
    expect(next).toHaveBeenCalledOnce();

    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    select.value = select.options[1].value;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(size).toHaveBeenCalledWith(25);

    setPaginationInputs(fixture, 2, 2);
    fixture.detectChanges();
    expect(buttons[0].disabled).toBe(false);
    expect(buttons[1].disabled).toBe(true);
  });

  it('renders a sale table and forwards all pagination events', () => {
    const fixture = TestBed.createComponent(SalesTable);
    const component = fixture.componentInstance;
    fixture.componentRef.setInput('result', result());
    fixture.componentRef.setInput('pageSize', 10);
    fixture.componentRef.setInput('returnUrl', '/sales?_page=2');
    const previous = vi.fn();
    const next = vi.fn();
    const size = vi.fn();
    component.previousPage.subscribe(previous);
    component.nextPage.subscribe(next);
    component.pageSizeChange.subscribe(size);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('SALE-1');
    expect(fixture.nativeElement.textContent).toContain('Customer');
    const pagination = fixture.debugElement.children[0].children.at(-1)
      ?.componentInstance as SalesPagination;
    pagination.previous.emit();
    pagination.next.emit();
    pagination.pageSizeChange.emit(50);
    expect(previous).toHaveBeenCalledOnce();
    expect(next).toHaveBeenCalledOnce();
    expect(size).toHaveBeenCalledWith(50);
  });

  it('renders the page action with the originating return URL', () => {
    const fixture = TestBed.createComponent(SalesPageHeader);
    fixture.componentRef.setInput('returnUrl', '/sales?_page=2');
    fixture.detectChanges();
    const link = fixture.nativeElement.querySelector('a');
    expect(link.textContent).toContain('New sale');
    expect(link.getAttribute('href')).toContain('returnUrl=%2Fsales%3F_page%3D2');
  });
});

function setPaginationInputs(
  fixture: ComponentFixture<SalesPagination>,
  currentPage: number,
  totalPages: number,
): void {
  fixture.componentRef.setInput('totalItems', 2);
  fixture.componentRef.setInput('currentPage', currentPage);
  fixture.componentRef.setInput('totalPages', totalPages);
  fixture.componentRef.setInput('pageSize', 10);
}

function result(): PagedSales {
  return {
    totalItems: 1,
    currentPage: 1,
    totalPages: 1,
    data: [
      {
        id: 'sale-id',
        saleNumber: 'SALE-1',
        saleDate: '2026-09-24T10:00:00Z',
        customer: { externalId: 'C-1', name: 'Customer' },
        branch: { externalId: 'B-1', name: 'Branch' },
        totalAmount: 36,
        isCancelled: false,
        updatedAt: '2026-09-24T10:00:01Z',
        version: 1,
      },
    ],
  };
}
