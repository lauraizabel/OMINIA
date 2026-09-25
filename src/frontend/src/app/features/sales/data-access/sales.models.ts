export interface ExternalIdentity {
  externalId: string;
  name: string;
}

export interface SaleSummary {
  id: string;
  saleNumber: string;
  saleDate: string;
  customer: ExternalIdentity;
  branch: ExternalIdentity;
  totalAmount: number;
  isCancelled: boolean;
  updatedAt: string;
  version: number;
}

export interface SaleItem {
  id: string;
  product: ExternalIdentity;
  quantity: number;
  unitPrice: number;
  discountRate: number;
  grossAmount: number;
  discountAmount: number;
  totalAmount: number;
  effectiveAmount: number;
  isCancelled: boolean;
  cancelledAt: string | null;
}

export interface Sale extends SaleSummary {
  items: SaleItem[];
  cancelledAt: string | null;
  createdAt: string;
}

export interface PagedSales {
  data: SaleSummary[];
  totalItems: number;
  currentPage: number;
  totalPages: number;
}

export interface SaleResource {
  sale: Sale;
  etag: string;
}

export interface SaleListQuery {
  page: number;
  size: number;
  order: string;
  saleNumber?: string;
  customerExternalId?: string;
  branchExternalId?: string;
  isCancelled?: boolean;
  minSaleDate?: string;
  maxSaleDate?: string;
  minTotalAmount?: string;
  maxTotalAmount?: string;
}

export interface SaleItemInput {
  id?: string;
  product: ExternalIdentity;
  quantity: number;
  unitPrice: number;
}

export interface CreateSaleRequest {
  saleNumber: string;
  saleDate: string;
  customer: ExternalIdentity;
  branch: ExternalIdentity;
  items: Omit<SaleItemInput, 'id'>[];
}

export interface UpdateSaleRequest {
  saleDate: string;
  customer: ExternalIdentity;
  branch: ExternalIdentity;
  items: SaleItemInput[];
}
