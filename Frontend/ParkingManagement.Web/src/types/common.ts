export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

export interface ApiError {
  message: string;
  statusCode?: number;
  details?: ProblemDetails;
}
