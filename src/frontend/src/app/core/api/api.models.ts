export interface ApiEnvelope<T> {
  success: boolean;
  message: string;
  data: T;
}

export interface ApiProblemDetail {
  field: string;
  code: string;
  message: string;
}

export interface ApiProblem {
  type: string;
  error: string;
  detail: string;
  traceId: string;
  errors?: ApiProblemDetail[];
}
