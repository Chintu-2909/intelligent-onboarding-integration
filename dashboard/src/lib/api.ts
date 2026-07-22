import type {
  AiFailureExplanation,
  CreateOnboardingRequest,
  HealthResponse,
  OnboardingSummary,
  OnboardingTransaction,
  PagedResponse,
} from "@/types/onboarding";

const API_BASE_URL =
  process.env.NEXT_PUBLIC_ONBOARDING_API_URL ??
  "http://localhost:5083";

export interface TransactionQuery {
  pageNumber?: number;
  pageSize?: number;
  status?: string;
  search?: string;
}

export class ApiError extends Error {
  status: number;
  correlationId: string | null;

  constructor(
    message: string,
    status: number,
    correlationId: string | null,
  ) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.correlationId = correlationId;
  }
}

function createCorrelationId(): string {
  if (
    typeof crypto !== "undefined" &&
    typeof crypto.randomUUID === "function"
  ) {
    return crypto.randomUUID().replaceAll("-", "");
  }

  return `dashboard-${Date.now()}`;
}

async function apiRequest<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const correlationId = createCorrelationId();

  const headers = new Headers(options.headers);

  headers.set(
    "X-Correlation-ID",
    correlationId,
  );

  if (options.body && !headers.has("Content-Type")) {
    headers.set(
      "Content-Type",
      "application/json",
    );
  }

  const response = await fetch(
    `${API_BASE_URL}${path}`,
    {
      ...options,
      headers,
      cache: "no-store",
    },
  );

  const responseCorrelationId =
    response.headers.get(
      "X-Correlation-ID",
    );

  if (!response.ok) {
    let message =
      `Request failed with status ${response.status}.`;

    try {
      const problem =
        (await response.json()) as {
          title?: string;
          detail?: string;
          message?: string;
          errors?: Record<string, string[]>;
        };

      if (problem.message) {
        message = problem.message;
      } else if (problem.detail) {
        message = problem.detail;
      } else if (problem.title) {
        message = problem.title;
      }

      if (problem.errors) {
        const validationMessages =
          Object.values(problem.errors)
            .flat()
            .join(" ");

        if (validationMessages) {
          message = validationMessages;
        }
      }
    } catch {
      // Keep the safe default error message.
    }

    throw new ApiError(
      message,
      response.status,
      responseCorrelationId,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export async function createOnboardingTransaction(
  request: CreateOnboardingRequest,
): Promise<OnboardingTransaction> {
  return apiRequest<OnboardingTransaction>(
    "/api/onboarding",
    {
      method: "POST",
      body: JSON.stringify(request),
    },
  );
}


export async function getOnboardingSummary():
  Promise<OnboardingSummary> {
  return apiRequest<OnboardingSummary>(
    "/api/onboarding/summary",
  );
}

export async function getTransactions(
  query: TransactionQuery = {},
): Promise<PagedResponse<OnboardingTransaction>> {
  const searchParameters =
    new URLSearchParams();

  searchParameters.set(
    "pageNumber",
    String(query.pageNumber ?? 1),
  );

  searchParameters.set(
    "pageSize",
    String(query.pageSize ?? 10),
  );

  if (query.status?.trim()) {
    searchParameters.set(
      "status",
      query.status.trim(),
    );
  }

  if (query.search?.trim()) {
    searchParameters.set(
      "search",
      query.search.trim(),
    );
  }

  return apiRequest<
    PagedResponse<OnboardingTransaction>
  >(
    `/api/onboarding?${searchParameters.toString()}`,
  );
}

export async function getTransaction(
  transactionId: string,
): Promise<OnboardingTransaction> {
  return apiRequest<OnboardingTransaction>(
    `/api/onboarding/${encodeURIComponent(
      transactionId,
    )}`,
  );
}

export async function processTransaction(
  transactionId: string,
): Promise<OnboardingTransaction> {
  return apiRequest<OnboardingTransaction>(
    `/api/onboarding/${encodeURIComponent(
      transactionId,
    )}/process`,
    {
      method: "POST",
    },
  );
}

export async function retryTransaction(
  transactionId: string,
): Promise<OnboardingTransaction> {
  return apiRequest<OnboardingTransaction>(
    `/api/onboarding/${encodeURIComponent(
      transactionId,
    )}/retry`,
    {
      method: "POST",
    },
  );
}

export async function explainFailure(
  transactionId: string,
): Promise<AiFailureExplanation> {
  return apiRequest<AiFailureExplanation>(
    `/api/onboarding/${encodeURIComponent(
      transactionId,
    )}/explain`,
    {
      method: "POST",
    },
  );
}

export async function getReadiness():
  Promise<HealthResponse> {
  return apiRequest<HealthResponse>(
    "/health/ready",
  );
}
