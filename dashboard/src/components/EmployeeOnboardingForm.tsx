"use client";

import {
  useState,
  type FormEvent,
} from "react";

import {
  ApiError,
  createOnboardingTransaction,
  processTransaction,
} from "@/lib/api";

import type {
  CreateOnboardingRequest,
  OnboardingTransaction,
} from "@/types/onboarding";

interface EmployeeOnboardingFormProps {
  onClose: () => void;

  onCreated: (
    transaction: OnboardingTransaction,
  ) => void;
}

const initialForm: CreateOnboardingRequest = {
  employeeNumber: "",
  firstName: "",
  lastName: "",
  email: "",
  department: "",
  country: "India",
  joiningDate: "",
};

export default function EmployeeOnboardingForm({
  onClose,
  onCreated,
}: EmployeeOnboardingFormProps) {
  const [form, setForm] =
    useState<CreateOnboardingRequest>(
      initialForm,
    );

  const [isSubmitting, setIsSubmitting] =
    useState(false);

  const [error, setError] =
    useState<string | null>(null);

 function updateField(
  field: keyof CreateOnboardingRequest,
  value: string,
) {
  setForm((current) => ({
    ...current,
    [field]: value,
  }));
}

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

    return "The employee onboarding request could not be submitted.";
  }

  function validateForm(): string | null {
    if (!form.employeeNumber.trim()) {
      return "Employee number is required.";
    }

    if (!form.firstName.trim()) {
      return "First name is required.";
    }

    if (!form.lastName.trim()) {
      return "Last name is required.";
    }

    if (!form.email.trim()) {
      return "Email address is required.";
    }

    if (
      !form.email.includes("@") ||
      !form.email.includes(".")
    ) {
      return "Enter a valid email address.";
    }

    if (!form.department.trim()) {
      return "Department is required.";
    }

    if (!form.country.trim()) {
      return "Country is required.";
    }

    if (!form.joiningDate) {
      return "Joining date is required.";
    }

    return null;
  }

  async function submitRequest(
    shouldProcess: boolean,
  ) {
    const validationError =
      validateForm();

    if (validationError) {
      setError(validationError);
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      const request: CreateOnboardingRequest =
        {
          employeeNumber:
            form.employeeNumber.trim(),

          firstName:
            form.firstName.trim(),

          lastName:
            form.lastName.trim(),

          email:
            form.email.trim(),

          department:
            form.department.trim(),

          country:
            form.country.trim(),

          joiningDate:
            form.joiningDate,
        };

      const createdTransaction =
        await createOnboardingTransaction(
          request,
        );

      if (!shouldProcess) {
        onCreated(createdTransaction);
        return;
      }

      const processedTransaction =
        await processTransaction(
          createdTransaction.transactionId,
        );

      onCreated(processedTransaction);
    } catch (requestError) {
      setError(
        buildErrorMessage(requestError),
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  function handleSubmit(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault();

    void submitRequest(false);
  }

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
        className="transaction-drawer onboarding-form-drawer"
        aria-label="Onboard a new employee"
      >
        <div className="drawer-header">
          <div>
            <p className="eyebrow">
              New employee request
            </p>

            <h2>
              Onboard Employee
            </h2>

            <p>
              Create a validated onboarding
              transaction and send it to the
              downstream HCM simulator.
            </p>
          </div>

          <button
            className="drawer-close"
            type="button"
            aria-label="Close employee onboarding form"
            onClick={onClose}
          >
            ×
          </button>
        </div>

        {error && (
          <div
            className="action-message action-error"
            role="alert"
          >
            {error}
          </div>
        )}

        <form
          className="onboarding-form"
          onSubmit={handleSubmit}
        >
          <section className="detail-section">
            <h3>Employee identity</h3>

            <div className="form-grid">
              <label>
                <span>Employee number</span>

                <input
                  required
                  maxLength={50}
                  placeholder="EMP-8001"
                  value={form.employeeNumber}
                  onChange={(event) =>
                    updateField(
                      "employeeNumber",
                      event.target.value,
                    )
                  }
                />
              </label>

              <label>
                <span>First name</span>

                <input
                  required
                  maxLength={100}
                  placeholder="Priya"
                  value={form.firstName}
                  onChange={(event) =>
                    updateField(
                      "firstName",
                      event.target.value,
                    )
                  }
                />
              </label>

              <label>
                <span>Last name</span>

                <input
                  required
                  maxLength={100}
                  placeholder="Sharma"
                  value={form.lastName}
                  onChange={(event) =>
                    updateField(
                      "lastName",
                      event.target.value,
                    )
                  }
                />
              </label>

              <label>
                <span>Email address</span>

                <input
                  required
                  type="email"
                  maxLength={200}
                  placeholder="priya.sharma@example.com"
                  value={form.email}
                  onChange={(event) =>
                    updateField(
                      "email",
                      event.target.value,
                    )
                  }
                />
              </label>
            </div>
          </section>

          <section className="detail-section">
            <h3>Employment information</h3>

            <div className="form-grid">
              <label>
                <span>Department</span>

                <input
                  required
                  maxLength={100}
                  placeholder="Engineering"
                  value={form.department}
                  onChange={(event) =>
                    updateField(
                      "department",
                      event.target.value,
                    )
                  }
                />
              </label>

              <label>
                <span>Country</span>

                <input
                  required
                  maxLength={100}
                  placeholder="India"
                  value={form.country}
                  onChange={(event) =>
                    updateField(
                      "country",
                      event.target.value,
                    )
                  }
                />
              </label>

              <label className="form-full-width">
                <span>Joining date</span>

                <input
                  required
                  type="date"
                  value={form.joiningDate}
                  onChange={(event) =>
                    updateField(
                      "joiningDate",
                      event.target.value,
                    )
                  }
                />
              </label>
            </div>
          </section>

          <section className="detail-section">
            <h3>Submission option</h3>

            <p className="form-guidance">
              Create only saves the transaction
              with Pending status. Create and
              process immediately sends the
              employee to Mock HCM.
            </p>

            <div className="form-actions">
              <button
                className="secondary-form-action"
                type="submit"
                disabled={isSubmitting}
              >
                {isSubmitting
                  ? "Submitting..."
                  : "Create only"}
              </button>

              <button
                className="primary-action"
                type="button"
                disabled={isSubmitting}
                onClick={() =>
                  void submitRequest(true)
                }
              >
                {isSubmitting
                  ? "Processing..."
                  : "Create and process"}
              </button>
            </div>
          </section>
        </form>
      </aside>
    </div>
  );
}
