import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api-base-url';
import { SalesApiService } from './sales-api.service';
import { Sale } from './sales.models';

describe('SalesApiService', () => {
  let service: SalesApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    service = TestBed.inject(SalesApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('serializes list filters with the API parameter names', () => {
    service
      .list({
        page: 2,
        size: 25,
        order: 'totalAmount desc',
        saleNumber: 'SALE-*',
        isCancelled: false,
        minTotalAmount: '10.00',
      })
      .subscribe();

    const request = http.expectOne(
      (candidate) => candidate.url === '/api/sales' && candidate.params.get('_page') === '2',
    );
    expect(request.request.params.get('_size')).toBe('25');
    expect(request.request.params.get('_order')).toBe('totalAmount desc');
    expect(request.request.params.get('saleNumber')).toBe('SALE-*');
    expect(request.request.params.get('isCancelled')).toBe('false');
    expect(request.request.params.get('_minTotalAmount')).toBe('10.00');
    request.flush({ data: [], totalItems: 0, currentPage: 2, totalPages: 0 });
  });

  it('keeps an ETag as an opaque value when reading and updating', async () => {
    const read = firstValueFrom(service.get('sale-id'));
    http.expectOne('/api/sales/sale-id').flush(sale(), { headers: { ETag: '"opaque-v7"' } });
    const resource = await read;
    expect(resource.etag).toBe('"opaque-v7"');

    service
      .update(
        'sale-id',
        {
          saleDate: resource.sale.saleDate,
          customer: resource.sale.customer,
          branch: resource.sale.branch,
          items: [],
        },
        resource.etag,
      )
      .subscribe();
    const update = http.expectOne('/api/sales/sale-id');
    expect(update.request.headers.get('If-Match')).toBe('"opaque-v7"');
    update.flush(sale(), { headers: { ETag: '"opaque-v8"' } });
  });

  it('sends the current ETag for item cancellation and deletion', () => {
    service.cancelItem('sale-id', 'item-id', '"v3"').subscribe();
    const cancellation = http.expectOne('/api/sales/sale-id/items/item-id/cancel');
    expect(cancellation.request.headers.get('If-Match')).toBe('"v3"');
    cancellation.flush(sale(), { headers: { ETag: '"v4"' } });

    service.delete('sale-id', '"v4"').subscribe();
    const deletion = http.expectOne('/api/sales/sale-id');
    expect(deletion.request.method).toBe('DELETE');
    expect(deletion.request.headers.get('If-Match')).toBe('"v4"');
    deletion.flush(null);
  });

  it('rejects a successful resource response without an ETag', async () => {
    const result = firstValueFrom(service.create(request())).catch((error: unknown) => error);
    http.expectOne('/api/sales').flush(sale());
    expect(await result).toBeInstanceOf(Error);
  });
});

function request() {
  return {
    saleNumber: 'SALE-1',
    saleDate: '2026-09-24T10:00:00Z',
    customer: { externalId: 'C-1', name: 'Customer' },
    branch: { externalId: 'B-1', name: 'Branch' },
    items: [{ product: { externalId: 'P-1', name: 'Product' }, quantity: 4, unitPrice: 10 }],
  };
}

function sale(): Sale {
  return {
    id: 'sale-id',
    saleNumber: 'SALE-1',
    saleDate: '2026-09-24T10:00:00Z',
    customer: { externalId: 'C-1', name: 'Customer' },
    branch: { externalId: 'B-1', name: 'Branch' },
    items: [],
    totalAmount: 0,
    isCancelled: false,
    cancelledAt: null,
    createdAt: '2026-09-24T10:00:01Z',
    updatedAt: '2026-09-24T10:00:01Z',
    version: 1,
  };
}
