"use client";

import {
  useCallback,
  useEffect,
  useState,
} from "react";

import EmployeeOnboardingForm from "@/components/EmployeeOnboardingForm";
import TransactionActionPanel from "@/components/TransactionActionPanel";

import {
  ApiError,
  getOnboardingSummary,
  getReadiness,
  getTransactions,
} from "@/lib/api";

import type {
  HealthResponse,
  OnboardingSummary,
  OnboardingTransaction,
  PagedResponse,
} from "@/types/onboarding";

const PAGE_SIZE = 5;

const statusOptions = [
  "",
  "Pending",
  "Processing",
  "Completed",
  "ValidationFailed",
  "TemporaryFailure",
  "TimedOut",
  "Failed",
  "RetryLimitExceeded",
];

function formatStatus(status: string): string {
  return status.replace(
    /([a-z])([A-Z])/g,
    "$1 $2",
  );
}

function formatDate(value: string | null): string {
  if (!value) {
    return "Not available";
  }

  return new Intl.DateTimeFormat(
    "en-IN",
    {
      dateStyle: "medium",
      timeStyle: "short",
    },
  ).format(new Date(value));
}

function getStatusClass(status: string): string {
  switch (status) {
    case "Completed":
      return "status status-success";

    case "Pending":
    case "Processing":
      return "status status-pending";

    case "TemporaryFailure":
    case "TimedOut":
      return "status status-warning";

    default:
      return "status status-failure";
  }
}

export default function Home() {
  const [summary, setSummary] =
    useState<OnboardingSummary | null>(null);

  const [transactions, setTransactions] =
    useState<PagedResponse<OnboardingTransaction> | null>(
      null,
    );

  const [health, setHealth] =
    useState<HealthResponse | null>(null);

  const [pageNumber, setPageNumber] =
    useState(1);

  const [status, setStatus] =
    useState("");

  const [searchInput, setSearchInput] =
    useState("");

  const [search, setSearch] =
    useState("");

  const [isLoading, setIsLoading] =
    useState(true);

  const [error, setError] =
    useState<string | null>(null);

  const [
  selectedTransaction,
  setSelectedTransaction,
] =
  useState<OnboardingTransaction | null>(
    null,
  );

  const [
  isOnboardingFormOpen,
  setIsOnboardingFormOpen,
] = useState(false);

  const loadDashboard =
    useCallback(async () => {
      setIsLoading(true);
      setError(null);

      try {
        const [
          summaryResponse,
          transactionResponse,
          healthResponse,
        ] = await Promise.all([
          getOnboardingSummary(),

          getTransactions({
            pageNumber,
            pageSize: PAGE_SIZE,
            status,
            search,
          }),

          getReadiness(),
        ]);

        setSummary(summaryResponse);
        setTransactions(transactionResponse);
        setHealth(healthResponse);
      } catch (requestError) {
        if (requestError instanceof ApiError) {
          const correlationMessage =
            requestError.correlationId
              ? ` Correlation ID: ${requestError.correlationId}`
              : "";

          setError(
            `${requestError.message}${correlationMessage}`,
          );
        } else {
          setError(
            "The dashboard could not connect to the Onboarding API.",
          );
        }
      } finally {
        setIsLoading(false);
      }
    }, [
      pageNumber,
      search,
      status,
    ]);

useEffect(() => {
  const timeoutId =
    window.setTimeout(() => {
      void loadDashboard();
    }, 0);

  return () => {
    window.clearTimeout(timeoutId);
  };
}, [loadDashboard]);

  function submitSearch(
    event: React.FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault();
    setPageNumber(1);
    setSearch(searchInput.trim());
  }

  function clearFilters() {
    setStatus("");
    setSearchInput("");
    setSearch("");
    setPageNumber(1);
  }

  async function handleTransactionUpdated(
  updatedTransaction: OnboardingTransaction,
) {
  setSelectedTransaction(
    updatedTransaction,
  );

  await loadDashboard();
}

async function handleEmployeeCreated(
  transaction: OnboardingTransaction,
) {
  setIsOnboardingFormOpen(false);
  setSelectedTransaction(transaction);
  setPageNumber(1);
  setStatus("");
  setSearch("");
  setSearchInput("");

  await loadDashboard();
}

  const summaryCards = [
    {
      label: "Total transactions",
      value:
        summary?.totalTransactions ?? 0,
      tone: "blue",
    },
    {
      label: "Completed",
      value:
        summary?.completedTransactions ?? 0,
      tone: "green",
    },
    {
      label: "Failed",
      value:
        summary?.failedTransactions ?? 0,
      tone: "red",
    },
    {
      label: "Retryable",
      value:
        summary?.retryableFailures ?? 0,
      tone: "amber",
    },
  ];

  return (
    <main className="dashboard-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">
            Intelligent integration operations
          </p>

          <h1>
            Employee Onboarding Command Center
          </h1>

          <p className="subtitle">
            Monitor onboarding transactions,
            downstream processing, controlled retries,
            and grounded AI guidance.
          </p>
        </div>

        <div className="header-actions">
          <button
  className="onboard-button"
  type="button"
  onClick={() =>
    setIsOnboardingFormOpen(true)
  }
>
  + Onboard Employee
</button>
          <div
            className={`health-pill ${
              health?.status === "Healthy"
                ? "health-healthy"
                : "health-degraded"
            }`}
          >
            <span className="health-dot" />

            {health?.status ??
              "Checking services"}
          </div>

          <button
            className="refresh-button"
            type="button"
            onClick={() =>
              void loadDashboard()
            }
            disabled={isLoading}
          >
            {isLoading
              ? "Refreshing..."
              : "Refresh"}
          </button>
        </div>
      </header>

      {error && (
        <section
          className="error-banner"
          role="alert"
        >
          <div>
            <strong>
              Dashboard connection issue
            </strong>

            <p>{error}</p>
          </div>

          <button
            type="button"
            onClick={() =>
              void loadDashboard()
            }
          >
            Try again
          </button>
        </section>
      )}

      <section className="summary-grid">
        {summaryCards.map((card) => (
          <article
            className={`summary-card summary-${card.tone}`}
            key={card.label}
          >
            <p>{card.label}</p>
            <strong>{card.value}</strong>
          </article>
        ))}
      </section>

      <section className="operations-panel">
        <div className="panel-heading">
          <div>
            <p className="eyebrow">
              Transaction operations
            </p>

            <h2>
              Onboarding transactions
            </h2>
          </div>

          <p className="result-count">
            {transactions?.totalItems ?? 0} results
          </p>
        </div>

        <div className="filters">
          <form
            className="search-form"
            onSubmit={submitSearch}
          >
            <input
              aria-label="Search transactions"
              type="search"
              placeholder="Search employee number or name"
              value={searchInput}
              onChange={(event) =>
                setSearchInput(
                  event.target.value,
                )
              }
            />

            <button type="submit">
              Search
            </button>
          </form>

          <select
            aria-label="Filter by status"
            value={status}
            onChange={(event) => {
              setStatus(event.target.value);
              setPageNumber(1);
            }}
          >
            {statusOptions.map(
              (statusOption) => (
                <option
                  key={
                    statusOption ||
                    "all-statuses"
                  }
                  value={statusOption}
                >
                  {statusOption
                    ? formatStatus(
                        statusOption,
                      )
                    : "All statuses"}
                </option>
              ),
            )}
          </select>

          <button
            className="clear-button"
            type="button"
            onClick={clearFilters}
          >
            Clear
          </button>
        </div>

        <div className="table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Employee</th>
                <th>Status</th>
                <th>HCM ID</th>
                <th>Attempts</th>
                <th>Last updated</th>
                <th>Actions</th>
              </tr>
            </thead>

            <tbody>
              {isLoading &&
                !transactions && (
                  <tr>
                    <td
                      colSpan={6}
                      className="empty-state"
                    >
                      Loading transactions...
                    </td>
                  </tr>
                )}

              {!isLoading &&
                transactions?.items.length ===
                  0 && (
                  <tr>
                    <td
                      colSpan={6}
                      className="empty-state"
                    >
                      No transactions matched
                      the selected filters.
                    </td>
                  </tr>
                )}

              {transactions?.items.map(
                (transaction) => (
                  <tr
                    key={
                      transaction.transactionId
                    }
                  >
                    <td>
                      <div className="employee-cell">
                        <strong>
                          {
                            transaction.employeeName
                          }
                        </strong>

                        <span>
                          {
                            transaction.employeeNumber
                          }
                        </span>
                      </div>
                    </td>

                    <td>
                      <span
                        className={getStatusClass(
                          transaction.status,
                        )}
                      >
                        {formatStatus(
                          transaction.status,
                        )}
                      </span>
                    </td>

                    <td>
                      {transaction.hcmEmployeeId ??
                        "Not assigned"}
                    </td>

                    <td>
                      {transaction.retryCount} / 3
                    </td>

                    <td>
                      {formatDate(
                        transaction.updatedAtUtc ??
                          transaction.createdAtUtc,
                      )}
                    </td>
                    
                    <td>
  <button
    className="details-button"
    type="button"
    onClick={() =>
      setSelectedTransaction(
        transaction,
      )
    }
  >
    View details
  </button>
</td>

                  </tr>
                ),
              )}
            </tbody>
          </table>
        </div>

        <div className="pagination">
          <button
            type="button"
            disabled={
              !transactions?.hasPreviousPage ||
              isLoading
            }
            onClick={() =>
              setPageNumber(
                (current) =>
                  Math.max(
                    1,
                    current - 1,
                  ),
              )
            }
          >
            Previous
          </button>

          <span>
            Page{" "}
            {transactions?.pageNumber ?? 1}
            {" of "}
            {Math.max(
              transactions?.totalPages ?? 1,
              1,
            )}
          </span>

          <button
            type="button"
            disabled={
              !transactions?.hasNextPage ||
              isLoading
            }
            onClick={() =>
              setPageNumber(
                (current) =>
                  current + 1,
              )
            }
          >
            Next
          </button>
        </div>
      </section>

      <footer>
        <span>
          API: localhost:5083
        </span>

        <span>
          Updated{" "}
          {summary
            ? formatDate(
                summary.generatedAtUtc,
              )
            : "when services connect"}
        </span>
      </footer>

    {isOnboardingFormOpen && (
  <EmployeeOnboardingForm
    onClose={() =>
      setIsOnboardingFormOpen(false)
    }
    onCreated={
      handleEmployeeCreated
    }
  />
)}
      {selectedTransaction && (
  <TransactionActionPanel
    transaction={selectedTransaction}
    onClose={() =>
      setSelectedTransaction(null)
    }
    onTransactionUpdated={
      handleTransactionUpdated
    }
  />
)}
    </main>
  );
}
