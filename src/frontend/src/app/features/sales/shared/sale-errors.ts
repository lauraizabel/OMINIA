import { HttpErrorResponse } from '@angular/common/http';
import { ApiProblem, ApiProblemDetail } from '../../../core/api/api.models';

export interface SaleErrorState {
  message: string;
  fields: Record<string, string>;
  conflict: boolean;
}

export function mapSaleError(error: unknown): SaleErrorState {
  if (!(error instanceof HttpErrorResponse)) {
    return {
      message: 'An unexpected error occurred. Please try again.',
      fields: {},
      conflict: false,
    };
  }

  const problem = isProblem(error.error) ? error.error : undefined;
  const messages: Record<number, string> = {
    0: 'The API is unavailable. Check your connection and verify the sale before resubmitting.',
    400: problem?.detail ?? 'Review the highlighted fields.',
    404: 'This sale no longer exists.',
    409: problem?.detail ?? 'The sale conflicts with the current state.',
    412: 'This sale changed after you opened it. Your changes were preserved.',
    428: 'The concurrency token is missing. Reload the sale before trying again.',
    429: 'Too many requests. Wait a moment and try again.',
    500: 'The server could not complete the request. Please try again.',
    503: 'A required service is unavailable. Please try again.',
  };

  return {
    message: messages[error.status] ?? problem?.detail ?? 'The request could not be completed.',
    fields: Object.fromEntries((problem?.errors ?? []).map(fieldEntry)),
    conflict: error.status === 412,
  };
}

function fieldEntry(detail: ApiProblemDetail): [string, string] {
  return [
    detail.field.replace(
      /(^|\.)([A-Z])/g,
      (_, prefix: string, letter: string) => `${prefix}${letter.toLowerCase()}`,
    ),
    detail.message,
  ];
}

function isProblem(value: unknown): value is ApiProblem {
  return typeof value === 'object' && value !== null && 'detail' in value;
}
