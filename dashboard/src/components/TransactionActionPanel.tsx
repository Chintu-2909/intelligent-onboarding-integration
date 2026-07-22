"use client";

import { useState } from "react";

import {
  ApiError,
  explainFailure,
  processTransaction,
  retryTransaction,
} from "@/lib/api";

import type {
  AiFailureExplanation,
  OnboardingTransaction,
} from "@/types/onboarding";

interface TransactionActionPanelProps {
  transaction: OnboardingTransaction;
  onClose: () => void;
  onTransactionUpdated: (
    transaction: OnboardingTransaction,
  ) => void;
}

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
      timeStyle: "medium",
    },
  ).format(new Date(value));
}

function canExplainFailure(
  transaction: OnboardingTransaction,
): boolean {
  return ![
    "Pending",
    "Processing",
    "Completed",
  ].includes(transaction.status);
}

export default function TransactionActionPanel({
  transaction,
  onClose,
  onTransactionUpdated,
}: TransactionActionPanelProps) {
  const [isProcessing, setIsProcessing] =
    useState(false);

  const [isRetrying, setIsRetrying] =
    useState(false);

  const [isExplaining, setIsExplaining] =
    useState(false);

  const [message, setMessage] =
    useState<string | null>(null);

  const [error, setError] =
    useState<string | null>(null);

  const [explanation, setExplanation] =
    useState<AiFailureExplanation | null>(
      null,
    );

  function buildErrorMessage(
    requestError: unknown,
  ): string {
    if (requestError instanceof ApiError) {
      const correlationMessage =
        requestError.correlationId
          ? ` Correlation ID: ${requestError.correlationId}`
          : "";

      return `${requestError.message}${correlationMessage}`;
    }

    return "The requested operation could not be completed.";
  }

  async function handleProcess() {
    setIsProcessing(true);
    setMessage(null);
    setError(null);

    try {
      const updatedTransaction =
        await processTransaction(
          transaction.transactionId,
        );

      setMessage(
        "The transaction was processed successfully.",
      );

      setExplanation(null);

      onTransactionUpdated(
        updatedTransaction,
      );
    } catch (requestError) {
      setError(
        buildErrorMessage(requestError),
      );
    } finally {
      setIsProcessing(false);
    }
  }

  async function handleRetry() {
    setIsRetrying(true);
    setMessage(null);
    setError(null);

    try {
      const updatedTransaction =
        await retryTransaction(
          transaction.transactionId,
        );

      setMessage(
        "The controlled retry was completed.",
      );

      setExplanation(null);

      onTransactionUpdated(
        updatedTransaction,
      );
    } catch (requestError) {
      setError(
        buildErrorMessage(requestError),
      );
    } finally {
      setIsRetrying(false);
    }
  }

  async function handleExplainFailure() {
    setIsExplaining(true);
    setMessage(null);
    setError(null);

    try {
      const explanationResponse =
        await explainFailure(
          transaction.transactionId,
        );

      setExplanation(
        explanationResponse,
      );

      setMessage(
        "Grounded operational guidance was generated.",
      );
    } catch (requestError) {
      setError(
        buildErrorMessage(requestError),
      );
    } finally {
      setIsExplaining(false);
    }
  }

  const anyActionRunning =
    isProcessing ||
    isRetrying ||
    isExplaining;

  return (
    <div
      className="drawer-backdrop"
      role="presentation"
      onMouseDown={(event) => {
        if (
          event.currentTarget ===
          event.target
        ) {
          onClose();
        }
      }}
    >
      <aside
        className="transaction-drawer"
        aria-label="Transaction details"
      >
        <div className="drawer-header">
          <div>
            <p className="eyebrow">
              Transaction details
            </p>

            <h2>
              {transaction.employeeName}
            </h2>

            <p>
              {transaction.employeeNumber}
            </p>
          </div>

          <button
            className="drawer-close"
            type="button"
            aria-label="Close transaction details"
            onClick={onClose}
          >
            ×
          </button>
        </div>

        {message && (
          <div
            className="action-message action-success"
            role="status"
          >
            {message}
          </div>
        )}

        {error && (
          <div
            className="action-message action-error"
            role="alert"
          >
            {error}
          </div>
        )}

        <section className="detail-section">
          <h3>Current state</h3>

          <div className="detail-grid">
            <div>
              <span>Status</span>
              <strong>
                {formatStatus(
                  transaction.status,
                )}
              </strong>
            </div>

            <div>
              <span>Retryable</span>
              <strong>
                {transaction.isRetryable
                  ? "Yes"
                  : "No"}
              </strong>
            </div>

            <div>
              <span>Attempts</span>
              <strong>
                {transaction.retryCount} / 3
              </strong>
            </div>

            <div>
              <span>HCM employee ID</span>
              <strong>
                {transaction.hcmEmployeeId ??
                  "Not assigned"}
              </strong>
            </div>
          </div>
        </section>

        <section className="detail-section">
          <h3>Processing information</h3>

          <dl className="detail-list">
            <div>
              <dt>Transaction ID</dt>
              <dd>
                {transaction.transactionId}
              </dd>
            </div>

            <div>
              <dt>Created</dt>
              <dd>
                {formatDate(
                  transaction.createdAtUtc,
                )}
              </dd>
            </div>

            <div>
              <dt>Last updated</dt>
              <dd>
                {formatDate(
                  transaction.updatedAtUtc,
                )}
              </dd>
            </div>

            <div>
              <dt>Last processing attempt</dt>
              <dd>
                {formatDate(
                  transaction.lastAttemptAtUtc,
                )}
              </dd>
            </div>
          </dl>
        </section>

        {(transaction.errorCode ||
          transaction.errorMessage) && (
          <section className="detail-section">
            <h3>Recorded failure</h3>

            <div className="failure-box">
              <span>
                {transaction.errorCode ??
                  "No error code"}
              </span>

              <p>
                {transaction.errorMessage ??
                  "No error message was recorded."}
              </p>
            </div>
          </section>
        )}

        <section className="detail-section">
          <h3>Available actions</h3>

          <div className="drawer-actions">
            {transaction.status ===
              "Pending" && (
              <button
                className="primary-action"
                type="button"
                onClick={() =>
                  void handleProcess()
                }
                disabled={anyActionRunning}
              >
                {isProcessing
                  ? "Processing..."
                  : "Process transaction"}
              </button>
            )}

            {transaction.isRetryable &&
              transaction.retryCount < 3 && (
                <button
                  className="retry-action"
                  type="button"
                  onClick={() =>
                    void handleRetry()
                  }
                  disabled={anyActionRunning}
                >
                  {isRetrying
                    ? "Retrying..."
                    : "Run controlled retry"}
                </button>
              )}

            {canExplainFailure(
              transaction,
            ) && (
              <button
                className="ai-action"
                type="button"
                onClick={() =>
                  void handleExplainFailure()
                }
                disabled={anyActionRunning}
              >
                {isExplaining
                  ? "Generating guidance..."
                  : "Explain failure with AI"}
              </button>
            )}

            {transaction.status ===
              "Completed" && (
              <p className="completed-note">
                This transaction has completed
                successfully. No further action
                is required.
              </p>
            )}
          </div>
        </section>

        {explanation && (
          <section className="detail-section">
            <div className="ai-heading">
              <div>
                <p className="eyebrow">
                  Grounded guidance
                </p>

                <h3>
                  Failure explanation
                </h3>
              </div>

              <span>
                {explanation.model}
              </span>
            </div>

            <pre className="ai-explanation">
              {explanation.explanation}
            </pre>

            <p className="ai-disclaimer">
              {explanation.disclaimer}
            </p>
          </section>
        )}
      </aside>
    </div>
  );
}
