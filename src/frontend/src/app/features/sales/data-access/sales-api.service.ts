import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../../core/config/api-base-url';
import {
  CreateSaleRequest,
  PagedSales,
  Sale,
  SaleListQuery,
  SaleResource,
  UpdateSaleRequest,
} from './sales.models';

@Injectable({ providedIn: 'root' })
export class SalesApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/sales`;

  list(query: SaleListQuery): Observable<PagedSales> {
    let params = new HttpParams()
      .set('_page', query.page)
      .set('_size', query.size)
      .set('_order', query.order);

    const values: [string, string | number | boolean | undefined][] = [
      ['saleNumber', query.saleNumber],
      ['customerExternalId', query.customerExternalId],
      ['branchExternalId', query.branchExternalId],
      ['isCancelled', query.isCancelled],
      ['_minSaleDate', query.minSaleDate],
      ['_maxSaleDate', query.maxSaleDate],
      ['_minTotalAmount', query.minTotalAmount],
      ['_maxTotalAmount', query.maxTotalAmount],
    ];

    for (const [key, value] of values) {
      if (value !== undefined && value !== '') params = params.set(key, value);
    }
    return this.http.get<PagedSales>(this.baseUrl, { params });
  }

  get(id: string): Observable<SaleResource> {
    return this.http
      .get<Sale>(`${this.baseUrl}/${id}`, { observe: 'response' })
      .pipe(map(toResource));
  }

  create(request: CreateSaleRequest): Observable<SaleResource> {
    return this.http
      .post<Sale>(this.baseUrl, request, { observe: 'response' })
      .pipe(map(toResource));
  }

  update(id: string, request: UpdateSaleRequest, etag: string): Observable<SaleResource> {
    return this.http
      .put<Sale>(`${this.baseUrl}/${id}`, request, {
        observe: 'response',
        headers: ifMatch(etag),
      })
      .pipe(map(toResource));
  }

  cancel(id: string, etag: string): Observable<SaleResource> {
    return this.http
      .post<Sale>(`${this.baseUrl}/${id}/cancel`, null, {
        observe: 'response',
        headers: ifMatch(etag),
      })
      .pipe(map(toResource));
  }

  cancelItem(id: string, itemId: string, etag: string): Observable<SaleResource> {
    return this.http
      .post<Sale>(`${this.baseUrl}/${id}/items/${itemId}/cancel`, null, {
        observe: 'response',
        headers: ifMatch(etag),
      })
      .pipe(map(toResource));
  }

  delete(id: string, etag: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`, { headers: ifMatch(etag) });
  }
}

function ifMatch(etag: string): HttpHeaders {
  return new HttpHeaders({ 'If-Match': etag });
}

function toResource(response: HttpResponse<Sale>): SaleResource {
  if (!response.body) throw new Error('The sales API returned an empty response.');
  const etag = response.headers.get('ETag');
  if (!etag) throw new Error('The sales API did not return the required ETag header.');
  return { sale: response.body, etag };
}
