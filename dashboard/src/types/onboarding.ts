export interface OnboardingTransaction {
  transactionId: string;
  employeeNumber: string;
  employeeName: string;
  status: string;
  hcmEmployeeId: string | null;
  errorCode: string | null;
  errorMessage: string | null;
  isRetryable: boolean;
  retryCount: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  lastAttemptAtUtc: string | null;
}

export interface CreateOnboardingRequest {
  employeeNumber: string;
  firstName: string;
  lastName: string;
  email: string;
  department: string;
  country: string;
  joiningDate: string;
}

export interface OnboardingSummary {
  totalTransactions: number;
  pendingTransactions: number;
  processingTransactions: number;
  completedTransactions: number;
  failedTransactions: number;
  retryableFailures: number;
  retryLimitExceededTransactions: number;
  generatedAtUtc: string;
}

export interface PagedResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface AiFailureExplanation {
  transactionId: string;
  status: string;
  errorCode: string | null;
  isRetryable: boolean;
  retryCount: number;
  explanation: string;
  model: string;
  generatedAtUtc: string;
  disclaimer: string;
}

export interface HealthCheckEntry {
  name: string;
  status: string;
  description: string | null;
  durationMilliseconds: number;
}

export interface HealthResponse {
  status: string;
  totalDurationMilliseconds: number;
  checks: HealthCheckEntry[];
  correlationId: string;
}
